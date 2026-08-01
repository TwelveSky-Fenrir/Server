-- Tribe conversion via consumable item (Book of Noble Dragon/Royal Serpent/Grand Tiger V2, world.Items
-- 99014/99015/99016). @ItemId alone derives the target tribe (99014->0, 99015->1, 99016->2) and is also
-- used to re-verify the level gate against world.Items itself -- the caller never supplies ToTribe
-- directly, closing a spoofing vector. @Container/@Items is a whole-container replace of the ONE inventory
-- container the book was consumed from (same convention as usp_Character_AdjustMoneyAndReplaceContainer):
-- the caller supplies every remaining slot of that container with the book's own slot simply absent, which
-- naturally reproduces "a stack of more than one book is entirely destroyed by a single use, not
-- decremented by one" (there is nothing to decrement -- the whole slot is just gone from the snapshot).
--
-- DB-checked preconditions (THROW on failure, in this order, first failure wins): ItemId is one of the 3
-- books (50313); character exists (50314); same-tribe rejection unless current tribe is the neutral/
-- "nangin" code 3 (50315); combined Level+Level2 meets the item's LevelLimit+MartialLevelLimit (50316); no
-- tribe office (master/sub-master/elected council seat, all 3 tiers) held in the CURRENT tribe (50317); no
-- guild (50318); no registered friends (50319); every equipped item (Container=2, all slots except
-- FEQUIP_TYPE.EPET=8) has a target-tribe equivalent in world.TribeItemEquivalences, or the whole conversion
-- aborts with no state changed (50320).
--
-- NOT checked here -- the CALLER must verify all three before invoking this procedure, since none has any
-- durable representation in this database at all: zone-37 cluster connectivity, IsValidTown (standing in
-- the character's own CURRENT tribe's capital), and no active party (party membership is pure in-memory
-- runtime state in this codebase, never persisted anywhere).
--
-- NOT persisted here, even on success -- these are Application-layer follow-ups against
-- world.usp_TribeConversionCatalog_GetAll's own equivalence data, not something this schema can remap today:
-- worn-costume wardrobe (PlayerRuntimeState.CostumeWardrobe has no backing table yet -- see
-- Tables/world/TribeCostumeEquivalences.sql's own remarks), auto-buff skill slots (pure runtime state, never
-- persisted), and the two auto-hunt skill-slot families (embedded inside game.Characters.AutoHuntConfig's
-- opaque VARBINARY blob, not something a plain UPDATE can remap).
--
-- Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:2119-2227 (full precondition chain, item consumption, tribe
-- commit) ; :167-238 (TChangeSkill) ; :509-585 (TChangeEquip, abort-on-no-equivalent at :555-562) ;
-- Server/Header/Protocol/STRUCT.h:1662-1676 (FEQUIP_TYPE, EPET=8).
CREATE PROCEDURE game.usp_Character_ApplyTribeConversion @CharacterId INT,
                                                         @ItemId INT,
                                                         @Container TINYINT,
                                                         @Items game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @ToTribe TINYINT = CASE @ItemId
                                   WHEN 99014 THEN 0
                                   WHEN 99015 THEN 1
                                   WHEN 99016 THEN 2
                                   ELSE NULL
        END;

    IF @ToTribe IS NULL
        THROW 50313, N'usp_Character_ApplyTribeConversion: ItemId is not one of the three tribe-conversion books (99014/99015/99016).', 1;

    DECLARE @CurrentTribe TINYINT, @PreviousTribe TINYINT, @Level SMALLINT, @Level2 SMALLINT;

    SELECT @CurrentTribe = Tribe,
           @PreviousTribe = PreviousTribe,
           @Level = Level,
           @Level2 = Level2
    FROM game.Characters
    WHERE CharacterId = @CharacterId;

    IF @@ROWCOUNT = 0
        THROW 50314, N'usp_Character_ApplyTribeConversion: unknown CharacterId.', 1;

    -- Precondition 1: same-tribe rejection, skipped entirely when the current tribe is the neutral/
    -- "nangin" code 3.
    IF @CurrentTribe <> 3 AND @PreviousTribe = @ToTribe
        THROW 50315, N'usp_Character_ApplyTribeConversion: target tribe already matches this character''s previous tribe.', 1;

    -- Precondition 2: level gate, derived from the consumed item's own catalog row -- also validates
    -- @ItemId genuinely exists in world.Items.
    DECLARE @LevelLimit SMALLINT, @MartialLevelLimit TINYINT;

    SELECT @LevelLimit = LevelLimit,
           @MartialLevelLimit = MartialLevelLimit
    FROM world.Items
    WHERE ItemId = @ItemId;

    IF @LevelLimit IS NULL OR (@Level + @Level2) < (@LevelLimit + @MartialLevelLimit)
        THROW 50316, N'usp_Character_ApplyTribeConversion: combined Level+Level2 does not meet the consumed item''s level requirement.', 1;

    -- Precondition 5: no tribe office held in the CURRENT tribe (master / sub-master / elected council
    -- seat -- all 3 tiers of ReturnTribeRole, unlike usp_TribeRole_GetForCharacter's own deliberately
    -- 2-tier scope).
    IF EXISTS (SELECT 1 FROM game.Tribes WHERE TribeId = @CurrentTribe AND MasterCharacterId = @CharacterId)
        OR EXISTS (SELECT 1 FROM game.TribeSubMasters WHERE TribeId = @CurrentTribe AND CharacterId = @CharacterId)
        OR EXISTS (SELECT 1 FROM game.TribeVotes WHERE TribeId = @CurrentTribe AND CandidateCharacterId = @CharacterId)
        THROW 50317, N'usp_Character_ApplyTribeConversion: character holds a tribe office in its current tribe.', 1;

    -- Precondition 7: no guild.
    IF EXISTS (SELECT 1 FROM game.GuildMembers WHERE CharacterId = @CharacterId)
        THROW 50318, N'usp_Character_ApplyTribeConversion: character belongs to a guild.', 1;

    -- Precondition 8: no friends registered.
    IF EXISTS (SELECT 1 FROM game.CharacterFriends WHERE CharacterId = @CharacterId)
        THROW 50319, N'usp_Character_ApplyTribeConversion: character has one or more registered friends.', 1;

    BEGIN TRANSACTION;

    IF @PreviousTribe <> @ToTribe
        BEGIN
            -- Step 1a: strict all-or-nothing equipped-gear remap (every Container=2 slot except the pet slot,
            -- FEQUIP_TYPE.EPET=8 -- already-empty slots have no row at all, so they never appear here). The
            -- 87129-87257 reserved band is the "V2" alternate, offset +129 from world.TribeItemEquivalences'
            -- own base values -- computed inline, not a separate table. Computed into a table variable first
            -- so the abort check runs before any UPDATE is issued (no partial equipment change is ever left
            -- behind).
            DECLARE @EquipMapped TABLE
                                 (
                                     Slot      TINYINT NOT NULL PRIMARY KEY,
                                     OldItemId INT     NOT NULL,
                                     NewItemId INT     NULL
                                 );

            INSERT INTO @EquipMapped (Slot, OldItemId, NewItemId)
            SELECT ci.Slot,
                   ci.ItemId,
                   tgt.ItemId + CASE WHEN ci.ItemId BETWEEN 87129 AND 87257 THEN 129 ELSE 0 END
            FROM game.CharacterItems AS ci
                     LEFT JOIN world.TribeItemEquivalences AS src
                               ON src.TribeId = @PreviousTribe
                                   AND src.ItemId = CASE
                                                        WHEN ci.ItemId BETWEEN 87129 AND 87257 THEN ci.ItemId - 129
                                                        ELSE ci.ItemId END
                     LEFT JOIN world.TribeItemEquivalences AS tgt
                               ON tgt.GroupIndex = src.GroupIndex AND tgt.TribeId = @ToTribe
            WHERE ci.CharacterId = @CharacterId
              AND ci.Container = 2
              AND ci.Slot <> 8; -- EPET, excluded from both the remap and the abort check

            IF EXISTS (SELECT 1 FROM @EquipMapped WHERE NewItemId IS NULL)
                THROW 50320, N'usp_Character_ApplyTribeConversion: an equipped item has no target-tribe equivalent.', 1;

            UPDATE ci
            SET ItemId = m.NewItemId
            FROM game.CharacterItems AS ci
                     JOIN @EquipMapped AS m ON m.Slot = ci.Slot
            WHERE ci.CharacterId = @CharacterId
              AND ci.Container = 2
              AND m.NewItemId <> m.OldItemId;

            -- Step 1c (partial -- see this file's own header for what is deliberately NOT covered): learned
            -- skills and skill-bound hotkeys (HotkeyBindingKind.Skill = Sort 1). Silent pass-through on no
            -- match, never aborts -- matches TChangeSkill's own no-failure-path posture.
            UPDATE cs
            SET SkillId = tgt.SkillId
            FROM game.CharacterSkills AS cs
                     JOIN world.TribeSkillEquivalences AS src
                          ON src.TribeId = @PreviousTribe AND src.SkillId = cs.SkillId
                     JOIN world.TribeSkillEquivalences AS tgt
                          ON tgt.GroupIndex = src.GroupIndex AND tgt.TribeId = @ToTribe
            WHERE cs.CharacterId = @CharacterId;

            UPDATE ch
            SET Value1 = tgt.SkillId
            FROM game.CharacterHotkeys AS ch
                     JOIN world.TribeSkillEquivalences AS src
                          ON src.TribeId = @PreviousTribe AND src.SkillId = ch.Value1
                     JOIN world.TribeSkillEquivalences AS tgt
                          ON tgt.GroupIndex = src.GroupIndex AND tgt.TribeId = @ToTribe
            WHERE ch.CharacterId = @CharacterId
              AND ch.Sort = 1;
            -- HotkeyBindingKind.Skill

            -- Step 1e + step 2: PreviousTribe only advances when step 1 actually ran; Tribe always advances.
            UPDATE game.Characters
            SET PreviousTribe = @ToTribe,
                Tribe         = @ToTribe,
                UpdatedAtUtc  = SYSUTCDATETIME()
            WHERE CharacterId = @CharacterId;
        END
    ELSE
        BEGIN
            -- Step 2 only: PreviousTribe already equals ToTribe, so the whole equip/skill/hotkey remap (step 1)
            -- is skipped entirely -- only the current Tribe field itself changes.
            UPDATE game.Characters
            SET Tribe        = @ToTribe,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE CharacterId = @CharacterId;
        END;

    -- Step 3: the consumed item's inventory slot -- whole-container replace, same convention as
    -- usp_CharacterItems_ReplaceContainer (caller supplies the full post-consumption container snapshot,
    -- with the book's own slot simply absent).
    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @Container;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                     Enchant, Combine, Refine, Socket,
                                     SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterId,
           @Container,
           Slot,
           ItemId,
           Quantity,
           Enchant,
           Combine,
           Refine,
           Socket,
           SocketGem1,
           SocketGem2,
           SocketGem3,
           ExpireDate,
           Serial
    FROM @Items;

    COMMIT TRANSACTION;
END;
