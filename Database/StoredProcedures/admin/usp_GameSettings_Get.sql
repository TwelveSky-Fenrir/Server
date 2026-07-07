-- Singleton row, seeded by 70_seed/admin/006_game_settings.sql. Cached in-memory by the caller
-- (GameSettingsRepository), not re-queried on every request.
CREATE PROCEDURE admin.usp_GameSettings_Get
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT ProxyShopDurationDays
    FROM admin.GameSettings
    WHERE Id = 1;
END;
