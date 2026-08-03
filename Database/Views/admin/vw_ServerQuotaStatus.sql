CREATE VIEW admin.vw_ServerQuotaStatus
AS
SELECT q.MaxPlayers,
       q.GagePlayerNum,
       s.CurrentPlayers
FROM admin.ServerQuota AS q
         CROSS JOIN (SELECT COUNT(*) AS CurrentPlayers FROM runtime.AccountSessions WITH (SNAPSHOT)) AS s;
