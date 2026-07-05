-- NULL @ExpiresAtUtc = permanent mute (until lifted). Not idempotent: every call inserts a new row,
-- even a repeat mute of the same target -- mutes are a log, not a single-row-per-target flag.
-- THROW 50306 if neither @AccountId nor @CharacterId is given (CK_Mutes_AccountOrCharacter is the
-- last-resort backstop under a race).
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
