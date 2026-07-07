-- NULL @ExpiresAtUtc = permanent ban. Not idempotent: every call inserts a new row, even a repeat ban
-- of the same target -- bans are a log, not a single-row-per-target flag (that's IsBanned's job).
-- THROW 50301 if neither @AccountId nor @CharacterId is given (CK_Bans_AccountOrCharacter is the
-- last-resort backstop under a race).
CREATE PROCEDURE admin.usp_Ban_Create @AccountId INT NULL,
                                      @CharacterId INT NULL,
                                      @Reason TINYINT,
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
