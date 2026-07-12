-- Continuously re-read by Fenrir.Application.Login.Hosting.ServerQuotaRefreshHost (~1s) and once, synchronously,
-- at LoginServer startup -- deliberately not cached here (unlike admin.usp_GameSetting_Get's 5-minute
-- AddInMemoryCache): the caller alone owns the refresh cadence.
CREATE PROCEDURE admin.usp_ServerQuota_GetMaxPlayers
AS
BEGIN
    SET NOCOUNT ON;

    SELECT MaxPlayers, GagePlayerNum
    FROM admin.ServerQuota
    WHERE Id = 1;
END;
