-- Contract: @AccountId INT NULL, @CharacterId INT NULL, @Reason TINYINT, @ExpiresAtUtc DATETIME2(3) NULL
--           -> RS0 { BanId INT } (scalar, identity of the newly created row).
-- NULL @ExpiresAtUtc = permanent ban (see admin.Bans's comment for the legacy epoch-0 mapping this
-- replaces). Not idempotent: every call inserts a new ban row, even a repeat ban of the same target --
-- bans are a log, not a single-row-per-target flag (that's auth.Accounts.IsBanned's job).
-- Errors: THROW 50301 if neither @AccountId nor @CharacterId is given (admin.ErrorCatalog, 503xx = admin
-- range) -- checked explicitly before the insert so the caller gets a documented error instead of the
-- raw CK_Bans_AccountOrCharacter violation (architecture reference §12.3: constraint violations are not
-- control flow; CK_Bans_AccountOrCharacter itself remains as the last-resort backstop under a race).
CREATE PROCEDURE admin.usp_Ban_Create @AccountId    INT NULL,
    @CharacterId  INT NULL,
    @Reason       TINYINT,
    @ExpiresAtUtc DATETIME2(3) = NULL
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    IF
@AccountId IS NULL AND @CharacterId IS NULL
        THROW 50301, N'A ban must target at least one of @AccountId or @CharacterId.', 1;

INSERT INTO admin.Bans (AccountId, CharacterId, Reason, ExpiresAtUtc)
    OUTPUT INSERTED.BanId
VALUES (@AccountId, @CharacterId, @Reason, @ExpiresAtUtc);
END;
