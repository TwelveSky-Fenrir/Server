-- Faction-transfer SCROLL tribe conversion (world.Items 8153/8154), the TChangeTribe reclassifier
-- (Server/ts25zone/S07_MyGame03.cpp:11525-11748) invoked from S04_MyWork03.cpp:4740-4856. This is the atomic,
-- hardened Fenrir replacement for the legacy "remap in memory, THEN separately RemoveItem" sequence: the
-- reclassification and the scroll consumption commit as ONE transaction, closing the grant-only exploit where
-- a client could keep the scroll after receiving the change (the stated required divergence from legacy in the
-- behavior contract's Atomicity section).
--
-- DISTINCT from -- and must never be conflated with -- game.usp_Character_ApplyTribeConversion (the Book of
-- Noble Dragon/Royal Serpent/Grand Tiger V2 mechanic, world.Items 99014/99015/99016). The two legacy paths are
-- genuinely different reclassifiers, not a duplicate:
--   * Book (TChangeEquip): ALL-OR-NOTHING equipment remap -- aborts the whole conversion if any occupied
--     non-pet slot has no target-tribe equivalent, and the SERVER derives the target tribe from the item id.
--   * Scroll (TChangeTribe, this procedure): BEST-EFFORT per slot -- an un-mappable slot is simply left as-is,
--     never an abort, and the target tribe is CLIENT-SUPPLIED (@ToTribe), so it is range-checked here (the
--     legacy "@ToTribe < 0 AND @ToTribe > 2" guard at S04_MyWork03.cpp:4745 is inert and never rejects
--     anything -- a hardening gap this procedure closes with a real 0..2 check, THROW 50342).
--
-- @Container/@Items is a whole-container replace of the ONE inventory container the scroll was consumed from
-- (same convention as usp_Character_ApplyTribeConversion / usp_Character_AdjustMoneyAndReplaceContainer): the
-- caller supplies every remaining slot of that container with the scroll's own slot simply absent, which
-- naturally reproduces "the scroll is consumed" without a separate decrement. Equipment (Container=2) is remap-
-- ped IN PLACE via UPDATE, never through this TVP -- exactly like the book procedure.
--
-- DB-checked preconditions (THROW on failure, first failure wins, in this order): @ItemId is one of the two
-- scrolls (50341); @ToTribe is a playable 0..2 tribe (50342); character exists (50343); @ToTribe does not
-- already equal the character's PreviousTribe -- the scroll's unconditional same-tribe rejection, with NO
-- neutral exemption unlike the book (50344); first-level meets LV_M33 = 145, i.e. max level, not merely "high
-- level" (50345, S04_MyWork03.cpp:4756 / Server/Header/Protocol/DEFINE.h:483); no tribe office held in the
-- CURRENT tribe -- master / sub-master / elected council seat (50346); no guild (50347); no registered friends
-- (50348).
--
-- NOT checked here -- the CALLER (the use-item service / a server-side gate) must verify these before invoking,
-- since none has a durable representation in this database and every one is pure in-memory runtime state:
-- zone-37 cluster connectivity, IsValidTown (standing in the current tribe's own capital), no active party, no
-- teacher/student relationship (the scroll DOES reject these, unlike the book -- but they live only in runtime
-- state), the equipped Cape/Cloak gate (needs the FEQUIP_TYPE cape slot constant, not opened for the contract),
-- and the synchronous ts25extra cross-process target-tribe validation (S06_MyUpperCom04.cpp:446-456, criteria
-- internal to ts25extra and out of scope). These mirror exactly the caller-owned split
-- usp_Character_ApplyTribeConversion already documents for the book path.
--
-- NOT persisted here, even on success (identical posture to usp_Character_ApplyTribeConversion, for the same
-- reasons): the worn-costume wardrobe remap (no durable per-character wardrobe table -- see
-- Tables/world/TribeCostumeEquivalences.sql), the deco-slot remap (the legacy tsDeco table has no Fenrir
-- equivalent -- only tsFation/costume was reverse-engineered), the auto-buff skill slots, and the two auto-hunt
-- skill-slot families (embedded in game.Characters.AutoHuntConfig's opaque VARBINARY blob). The equipment remap
-- below also does NOT apply the legacy TChangeTribe cape/deco/pet slot EXCLUSIONS byte-for-byte (only the pet
-- slot, FEQUIP_TYPE.EPET=8, is excluded, as in the book path) because the cape and 4 deco slot indices were not
-- opened for the contract; the best-effort INNER JOIN naturally leaves cape/deco gear untouched anyway, since
-- those items have no rows in world.TribeItemEquivalences (which seeds only cross-tribe-equivalent weapons/
-- armor). A byte-exact cape/deco exclusion is an Application-layer follow-up gated on those FEQUIP_TYPE
-- constants.
--
-- Réf. C++ : Server/ts25zone/S07_MyGame03.cpp:11525-11748 (TChangeTribe: best-effort equipment/skill/hotkey/
-- deco/costume remap, aPreviousTribe/aTribe both set to target) ; S04_MyWork03.cpp:4740-4856 (scroll dispatch,
-- gate chain, inert range guard :4745, LV_M33 gate :4756, RemoveItem :4847, B_RETURN_TO_AUTO_ZONE :4853-4854).
CREATE PROCEDURE game.usp_Character_ApplyTribeScrollConversion @CharacterId INT,
                                                               @ItemId INT,
                                                               @ToTribe TINYINT,
                                                               @Container TINYINT,
                                                               @Items game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Precondition 1: @ItemId must be one of the two faction-transfer scrolls. The scroll is identified by id
    -- (not derived like the book), so re-validate it server-side rather than trusting the caller's routing.
    IF @ItemId NOT IN (8153, 8154)
        THROW 50341, N'usp_Character_ApplyTribeScrollConversion: ItemId is not one of the two faction-transfer scrolls (8153/8154).', 1;

    -- Precondition 2 (HARDENING): the client-supplied target tribe must be a playable 0..2 tribe. Replaces the
    -- legacy inert "< 0 AND > 2" guard; without this a spoofed @ToTribe of 3+ would index the 3-wide remap
    -- tables out of range in the legacy code.
    IF @ToTribe > 2
        THROW 50342, N'usp_Character_ApplyTribeScrollConversion: target tribe is outside the playable 0..2 range.', 1;

    DECLARE @CurrentTribe TINYINT, @PreviousTribe TINYINT, @Level SMALLINT;

    SELECT @CurrentTribe = Tribe,
           @PreviousTribe = PreviousTribe,
           @Level = Level
    FROM game.Characters
    WHERE CharacterId = @CharacterId;

    IF @@ROWCOUNT = 0
        THROW 50343, N'usp_Character_ApplyTribeScrollConversion: unknown CharacterId.', 1;

    -- Precondition 3: unconditional same-tribe rejection (no neutral exemption, unlike the book path).
    IF @PreviousTribe = @ToTribe
        THROW 50344, N'usp_Character_ApplyTribeScrollConversion: target tribe already matches this character''s previous tribe.', 1;

    -- Precondition 4: first-level must be at the level cap LV_M33 = 145 (max level, "not merely high level").
    IF @Level < 145
        THROW 50345, N'usp_Character_ApplyTribeScrollConversion: first-level is below the required LV_M33 (145).', 1;

    -- Precondition 5: no tribe office held in the CURRENT tribe (master / sub-master / elected council seat --
    -- all 3 tiers, identical to the book path's own 3-tier ReturnTribeRole check).
    IF EXISTS (SELECT 1 FROM game.Tribes WHERE TribeId = @CurrentTribe AND MasterCharacterId = @CharacterId)
        OR EXISTS (SELECT 1 FROM game.TribeSubMasters WHERE TribeId = @CurrentTribe AND CharacterId = @CharacterId)
        OR EXISTS (SELECT 1 FROM game.TribeVotes WHERE TribeId = @CurrentTribe AND CandidateCharacterId = @CharacterId)
        THROW 50346, N'usp_Character_ApplyTribeScrollConversion: character holds a tribe office in its current tribe.', 1;

    -- Precondition 6: no guild.
    IF EXISTS (SELECT 1 FROM game.GuildMembers WHERE CharacterId = @CharacterId)
        THROW 50347, N'usp_Character_ApplyTribeScrollConversion: character belongs to a guild.', 1;

    -- Precondition 7: no registered friends.
    IF EXISTS (SELECT 1 FROM game.CharacterFriends WHERE CharacterId = @CharacterId)
        THROW 50348, N'usp_Character_ApplyTribeScrollConversion: character has one or more registered friends.', 1;

    BEGIN TRANSACTION;

    -- Step 1: BEST-EFFORT equipped-gear remap. INNER JOIN means ONLY slots that have a target-tribe equivalent
    -- are rewritten; every other occupied slot (cape, deco, tribe-neutral gear with no equivalence row, the pet
    -- slot which is excluded outright) is left exactly as-is -- no all-or-nothing abort, matching TChangeTribe's
    -- per-slot posture and the opposite of the book path's TChangeEquip. The 87129-87257 "V2" reserved band is
    -- resolved by the same +129 arithmetic the book path uses, computed inline (not a separate table).
    UPDATE ci
    SET ItemId = tgt.ItemId + CASE WHEN ci.ItemId BETWEEN 87129 AND 87257 THEN 129 ELSE 0 END
    FROM game.CharacterItems AS ci
             INNER JOIN world.TribeItemEquivalences AS src
                        ON src.TribeId = @PreviousTribe
                            AND src.ItemId = CASE
                                                 WHEN ci.ItemId BETWEEN 87129 AND 87257 THEN ci.ItemId - 129
                                                 ELSE ci.ItemId END
             INNER JOIN world.TribeItemEquivalences AS tgt
                        ON tgt.GroupIndex = src.GroupIndex AND tgt.TribeId = @ToTribe
    WHERE ci.CharacterId = @CharacterId
      AND ci.Container = 2
      AND ci.Slot <> 8; -- FEQUIP_TYPE.EPET (pet slot), excluded exactly like the book path

    -- Step 2: learned skills, best-effort (silent pass-through on no match -- TChangeTribe never fails here).
    UPDATE cs
    SET SkillId = tgt.SkillId
    FROM game.CharacterSkills AS cs
             INNER JOIN world.TribeSkillEquivalences AS src
                        ON src.TribeId = @PreviousTribe AND src.SkillId = cs.SkillId
             INNER JOIN world.TribeSkillEquivalences AS tgt
                        ON tgt.GroupIndex = src.GroupIndex AND tgt.TribeId = @ToTribe
    WHERE cs.CharacterId = @CharacterId;

    -- Step 3: skill-bound hotkeys (HotkeyBindingKind.Skill = Sort 1), same best-effort skill remap.
    UPDATE ch
    SET Value1 = tgt.SkillId
    FROM game.CharacterHotkeys AS ch
             INNER JOIN world.TribeSkillEquivalences AS src
                        ON src.TribeId = @PreviousTribe AND src.SkillId = ch.Value1
             INNER JOIN world.TribeSkillEquivalences AS tgt
                        ON tgt.GroupIndex = src.GroupIndex AND tgt.TribeId = @ToTribe
    WHERE ch.CharacterId = @CharacterId
      AND ch.Sort = 1;

    -- Step 4: TChangeTribe sets BOTH aPreviousTribe and aTribe to the target (S07_MyGame03.cpp:11745-11746).
    UPDATE game.Characters
    SET PreviousTribe = @ToTribe,
        Tribe         = @ToTribe,
        UpdatedAtUtc  = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;

    -- Step 5: consume the scroll -- whole-container replace of the container it was used from (the scroll's own
    -- slot is simply absent from @Items). This is the atomic counterpart to the legacy caller's separate
    -- RemoveItem (:4847), now committed together with the reclassification above.
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
