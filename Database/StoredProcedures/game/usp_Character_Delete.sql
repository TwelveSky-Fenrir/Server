CREATE PROCEDURE game.usp_Character_Delete @AccountId INT,
                                           @Slot TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @CharacterId INT = (SELECT CharacterId
                                FROM game.Characters
                                WHERE AccountId = @AccountId
                                  AND Slot = @Slot);

    IF @CharacterId IS NULL
        RETURN;

    BEGIN TRANSACTION;

    UPDATE game.Characters
    SET TeacherCharacterId = NULL
    WHERE TeacherCharacterId = @CharacterId;
    UPDATE game.Characters
    SET StudentCharacterId = NULL
    WHERE StudentCharacterId = @CharacterId;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId;
    DELETE
    FROM game.CharacterSkills
    WHERE CharacterId = @CharacterId;
    DELETE
    FROM game.CharacterHotkeys
    WHERE CharacterId = @CharacterId;
    DELETE
    FROM game.CharacterBuffs
    WHERE CharacterId = @CharacterId;
    DELETE
    FROM game.CharacterQuests
    WHERE CharacterId = @CharacterId;
    DELETE
    FROM game.CharacterFriends
    WHERE CharacterId = @CharacterId
       OR FriendCharacterId = @CharacterId;
    DELETE
    FROM game.CharacterPetBag
    WHERE CharacterId = @CharacterId;
    DELETE
    FROM game.CharacterRunes
    WHERE CharacterId = @CharacterId;
    DELETE
    FROM game.CharacterCostumes
    WHERE CharacterId = @CharacterId;

    DELETE
    FROM game.HeroRankings
    WHERE CharacterId = @CharacterId;
    DELETE
    FROM game.OfflineShops
    WHERE CharacterId = @CharacterId;

    UPDATE admin.Bans
    SET CharacterId = NULL
    WHERE CharacterId = @CharacterId
      AND AccountId IS NOT NULL;
    DELETE
    FROM admin.Bans
    WHERE CharacterId = @CharacterId
      AND AccountId IS NULL;

    UPDATE admin.Mutes
    SET CharacterId = NULL
    WHERE CharacterId = @CharacterId
      AND AccountId IS NOT NULL;
    DELETE
    FROM admin.Mutes
    WHERE CharacterId = @CharacterId
      AND AccountId IS NULL;

    UPDATE game.TribeBankLog
    SET CharacterId = NULL
    WHERE CharacterId = @CharacterId;

    DELETE
    FROM game.Characters
    WHERE CharacterId = @CharacterId;

    COMMIT TRANSACTION;
END;
