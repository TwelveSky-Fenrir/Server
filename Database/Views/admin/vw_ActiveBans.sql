-- "Active" = not expired (ExpiresAtUtc IS NULL, or still in the future). LEFT JOINs are for display only
-- (an account-wide or character-only ban still returns a row, with a NULL name on the side that doesn't apply).
-- Not an indexed-view candidate: SYSUTCDATETIME() in the WHERE clause is non-deterministic, which disqualifies
-- indexed views outright before write frequency even enters the analysis (and admin.Bans/auth.Accounts/
-- game.Characters are all actively written besides, so it would fail on that basis too).
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
