CREATE PROCEDURE game.usp_Character_ApplyTribeScrollConversion @CharacterId INT,
                                                               @ItemId INT,
                                                               @ToTribe TINYINT,
                                                               @Container TINYINT,
                                                               @Items game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ItemId NOT IN (8153, 8154)
        THROW 50341, N'usp_Character_ApplyTribeScrollConversion: ItemId is not one of the two faction-transfer scrolls (8153/8154).', 1;

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

    IF @PreviousTribe = @ToTribe
        THROW 50344, N'usp_Character_ApplyTribeScrollConversion: target tribe already matches this character''s previous tribe.', 1;

    IF @Level < 145
        THROW 50345, N'usp_Character_ApplyTribeScrollConversion: first-level is below the required LV_M33 (145).', 1;

    IF EXISTS (SELECT 1 FROM game.Tribes WHERE TribeId = @CurrentTribe AND MasterCharacterId = @CharacterId)
        OR EXISTS (SELECT 1 FROM game.TribeSubMasters WHERE TribeId = @CurrentTribe AND CharacterId = @CharacterId)
        OR EXISTS (SELECT 1 FROM game.TribeVotes WHERE TribeId = @CurrentTribe AND CandidateCharacterId = @CharacterId)
        THROW 50346, N'usp_Character_ApplyTribeScrollConversion: character holds a tribe office in its current tribe.', 1;

    IF EXISTS (SELECT 1 FROM game.GuildMembers WHERE CharacterId = @CharacterId)
        THROW 50347, N'usp_Character_ApplyTribeScrollConversion: character belongs to a guild.', 1;

    IF EXISTS (SELECT 1 FROM game.CharacterFriends WHERE CharacterId = @CharacterId)
        THROW 50348, N'usp_Character_ApplyTribeScrollConversion: character has one or more registered friends.', 1;

    BEGIN TRANSACTION;

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
      AND ci.Slot <> 8;

    UPDATE cs
    SET SkillId = tgt.SkillId
    FROM game.CharacterSkills AS cs
             INNER JOIN world.TribeSkillEquivalences AS src
                        ON src.TribeId = @PreviousTribe AND src.SkillId = cs.SkillId
             INNER JOIN world.TribeSkillEquivalences AS tgt
                        ON tgt.GroupIndex = src.GroupIndex AND tgt.TribeId = @ToTribe
    WHERE cs.CharacterId = @CharacterId;

    UPDATE ch
    SET Value1 = tgt.SkillId
    FROM game.CharacterHotkeys AS ch
             INNER JOIN world.TribeSkillEquivalences AS src
                        ON src.TribeId = @PreviousTribe AND src.SkillId = ch.Value1
             INNER JOIN world.TribeSkillEquivalences AS tgt
                        ON tgt.GroupIndex = src.GroupIndex AND tgt.TribeId = @ToTribe
    WHERE ch.CharacterId = @CharacterId
      AND ch.Sort = 1;

    UPDATE game.Characters
    SET PreviousTribe = @ToTribe,
        Tribe         = @ToTribe,
        UpdatedAtUtc  = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;

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
