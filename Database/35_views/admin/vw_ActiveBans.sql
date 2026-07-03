-- Readability view for the two hot-ish lookups (admin.usp_Ban_GetActiveForAccount/...ForCharacter):
-- "active" means not expired (ExpiresAtUtc IS NULL = permanent, or still in the future). LEFT JOINs to
-- auth.Accounts/game.Characters are purely for display (LoginName/CharacterName) -- a ban row with a
-- NULL CharacterId (account-wide ban) or NULL AccountId (character-only ban) still returns a row, it
-- just has a NULL name on the side that doesn't apply. Internal use only (no direct grant, per the
-- §12.3 "no table/view rights" security model) -- both procedures below SELECT from this view instead
-- of repeating the expiry predicate.
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
