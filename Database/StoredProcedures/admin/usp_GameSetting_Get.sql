CREATE PROCEDURE admin.usp_GameSetting_Get
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT ProxyShopDurationDays
    FROM admin.GameSetting
    WHERE Id = 1;
END;
