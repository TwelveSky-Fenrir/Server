-- The handler is silent (no ZC reply); this proc is its only durable effect.
CREATE PROCEDURE game.usp_Character_SetAutoPotionThreshold @CharacterId INT,
                                                           @AutoLifeRatio TINYINT,
                                                           @AutoManaRatio TINYINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.Characters
    SET AutoLifeRatio = @AutoLifeRatio,
        AutoManaRatio = @AutoManaRatio,
        UpdatedAtUtc  = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;
END;
