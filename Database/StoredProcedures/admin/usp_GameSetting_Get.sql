-- Singleton row, seeded by 70_seed/admin/006_game_setting.sql. Cached in-memory by the caller
-- (GameSettingsRepository), not re-queried on every request.
CREATE PROCEDURE admin.usp_GameSetting_Get
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT ProxyShopDurationDays
    FROM admin.GameSetting
    WHERE Id = 1;
END;
