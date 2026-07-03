-- Contract: @AccountId INT NULL, @CharacterId INT NULL, @Reason TINYINT, @ExpiresAtUtc DATETIME2(3) NULL
--           -> RS0 { MuteId INT } (scalar, identity of the newly created row).
-- NULL @ExpiresAtUtc = permanent mute (until lifted). Not idempotent: every call inserts a new mute row,
-- even a repeat mute of the same target -- mutes are a log, not a single-row-per-target flag (mirrors
-- admin.usp_Ban_Create exactly; see admin.Mutes for why lifting is a column, not a deletion).
-- Errors: THROW 50306 if neither @AccountId nor @CharacterId is given (admin.ErrorCatalog, 503xx =
-- admin range) -- checked explicitly before the insert so the caller gets a documented error instead of
-- the raw CK_Mutes_AccountOrCharacter violation (§12.3: constraint violations are not control flow; the
-- CHECK remains as the last-resort backstop under a race).
CREATE PROCEDURE admin.usp_Mute_Create @AccountId    INT NULL,
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
        THROW 50306, N'A mute must target at least one of @AccountId or @CharacterId.', 1;

INSERT INTO admin.Mutes (AccountId, CharacterId, Reason, ExpiresAtUtc)
    OUTPUT INSERTED.MuteId
VALUES (@AccountId, @CharacterId, @Reason, @ExpiresAtUtc);
END;
