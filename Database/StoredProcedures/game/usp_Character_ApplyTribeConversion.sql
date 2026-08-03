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

    IF @CurrentTribe <> 3 AND @PreviousTribe = @ToTribe
        THROW 50315, N'usp_Character_ApplyTribeConversion: target tribe already matches this character''s previous tribe.', 1;

    DECLARE @LevelLimit SMALLINT, @MartialLevelLimit TINYINT;

    SELECT @LevelLimit = LevelLimit,
           @MartialLevelLimit = MartialLevelLimit
    FROM world.Items
    WHERE ItemId = @ItemId;

    IF @LevelLimit IS NULL OR (@Level + @Level2) < (@LevelLimit + @MartialLevelLimit)
        THROW 50316, N'usp_Character_ApplyTribeConversion: combined Level+Level2 does not meet the consumed item''s level requirement.', 1;

    IF EXISTS (SELECT 1 FROM game.Tribes WHERE TribeId = @CurrentTribe AND MasterCharacterId = @CharacterId)
        OR EXISTS (SELECT 1 FROM game.TribeSubMasters WHERE TribeId = @CurrentTribe AND CharacterId = @CharacterId)
        OR EXISTS (SELECT 1 FROM game.TribeVotes WHERE TribeId = @CurrentTribe AND CandidateCharacterId = @CharacterId)
        THROW 50317, N'usp_Character_ApplyTribeConversion: character holds a tribe office in its current tribe.', 1;

    IF EXISTS (SELECT 1 FROM game.GuildMembers WHERE CharacterId = @CharacterId)
        THROW 50318, N'usp_Character_ApplyTribeConversion: character belongs to a guild.', 1;

    IF EXISTS (SELECT 1 FROM game.CharacterFriends WHERE CharacterId = @CharacterId)
        THROW 50319, N'usp_Character_ApplyTribeConversion: character has one or more registered friends.', 1;

    BEGIN TRANSACTION;

    IF @PreviousTribe <> @ToTribe
        BEGIN
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
              AND ci.Slot <> 8; 

            IF EXISTS (SELECT 1 FROM @EquipMapped WHERE NewItemId IS NULL)
                THROW 50320, N'usp_Character_ApplyTribeConversion: an equipped item has no target-tribe equivalent.', 1;

            UPDATE ci
            SET ItemId = m.NewItemId
            FROM game.CharacterItems AS ci
                     JOIN @EquipMapped AS m ON m.Slot = ci.Slot
            WHERE ci.CharacterId = @CharacterId
              AND ci.Container = 2
              AND m.NewItemId <> m.OldItemId;

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

            UPDATE game.Characters
            SET PreviousTribe = @ToTribe,
                Tribe         = @ToTribe,
                UpdatedAtUtc  = SYSUTCDATETIME()
            WHERE CharacterId = @CharacterId;
        END
    ELSE
        BEGIN
            UPDATE game.Characters
            SET Tribe        = @ToTribe,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE CharacterId = @CharacterId;
        END;

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
