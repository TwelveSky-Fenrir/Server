
IF OBJECT_ID('game.usp_Character_UpdateAppearance', 'P') IS NOT NULL
    DROP PROCEDURE game.usp_Character_UpdateAppearance;
GO

CREATE PROCEDURE game.usp_Character_UpdateAppearance @CharacterId INT,
                                                     @HeadType    TINYINT,
                                                     @FaceType    TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE game.Characters
    SET HeadType     = @HeadType,
        FaceType     = @FaceType,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;

    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END;
END;
GO

IF OBJECT_ID('game.usp_Character_UpdateGenderAndAppearance', 'P') IS NOT NULL
    DROP PROCEDURE game.usp_Character_UpdateGenderAndAppearance;
GO

CREATE PROCEDURE game.usp_Character_UpdateGenderAndAppearance @CharacterId INT,
                                                              @Gender      TINYINT,
                                                              @HeadType    TINYINT,
                                                              @FaceType    TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE game.Characters
    SET Gender       = @Gender,
        HeadType     = @HeadType,
        FaceType     = @FaceType,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;

    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END;
END;
GO
