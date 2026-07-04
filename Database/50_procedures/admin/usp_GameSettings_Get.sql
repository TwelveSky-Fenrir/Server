-- database/50_procedures/admin/usp_GameSettings_Get.sql
-- Contract: no parameters -> RS0, exactly one row (the singleton admin.GameSettings row, seeded by
-- 70_seed/admin/006_game_settings.sql). Read-only, safe to retry. Cached in-memory by the caller
-- (GameSettingsRepository, a few minutes' TTL) -- not re-queried on every request that needs a setting.
CREATE PROCEDURE admin.usp_GameSettings_Get
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ProxyShopDurationDays
    FROM admin.GameSettings
    WHERE Id = 1;
END;
