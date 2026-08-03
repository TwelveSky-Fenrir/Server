CREATE VIEW admin.vw_ActiveSessionsByShard
AS
SELECT d.ShardId,
       d.Host,
       d.Port,
       d.Ccu,
       d.Capacity,
       d.TickP99Ms,
       d.LastHeartbeatUtc,
       ISNULL(s.TotalSessions, 0)       AS TotalSessions,
       ISNULL(s.ActiveSessions, 0)      AS ActiveSessions,
       ISNULL(s.TearingDownSessions, 0) AS TearingDownSessions,
       s.MostRecentSessionRefreshUtc
FROM runtime.GameServerDirectory AS d WITH (SNAPSHOT)
         LEFT JOIN (SELECT ShardId,
                           COUNT(*)                                          AS TotalSessions,
                           SUM(CASE WHEN SessionState = 0 THEN 1 ELSE 0 END) AS ActiveSessions,
                           SUM(CASE WHEN SessionState = 1 THEN 1 ELSE 0 END) AS TearingDownSessions,
                           MAX(LastRefreshedUtc)                             AS MostRecentSessionRefreshUtc
                    FROM runtime.AccountSessions WITH (SNAPSHOT)
                    WHERE ServerKind = 1
                    GROUP BY ShardId) AS s ON s.ShardId = d.ShardId;
