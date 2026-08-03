CREATE VIEW admin.vw_ActiveBans
AS
SELECT b.BanId,
       b.AccountId,
       a.LoginName AS AccountLoginName,
       b.CharacterId,
       c.Name      AS CharacterName,
       b.Reason,
       b.ExpiresAtUtc,
       b.CreatedAtUtc
FROM admin.Bans AS b
         LEFT JOIN auth.Accounts AS a ON a.AccountId = b.AccountId
         LEFT JOIN game.Characters AS c ON c.CharacterId = b.CharacterId
WHERE b.ExpiresAtUtc IS NULL
   OR b.ExpiresAtUtc > SYSUTCDATETIME();
