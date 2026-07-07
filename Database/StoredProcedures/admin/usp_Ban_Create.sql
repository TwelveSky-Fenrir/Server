-- NULL @ExpiresAtUtc = permanent ban. Not idempotent: every call inserts a new row, even a repeat ban
-- of the same target -- bans are a log, not a single-row-per-target flag (that's IsBanned's job).
-- THROW 50301 if neither @AccountId nor @CharacterId is given (CK_Bans_AccountOrCharacter is the
-- last-resort backstop under a race).
--
-- @ActorAccountId/@ActorCharacterId attribute the ban to the GM who issued it (independently nullable, same
-- shape as the target @AccountId/@CharacterId parameters -- a system-imposed/legacy-import ban legitimately
-- has no GM actor). Appended as trailing optional parameters (default NULL) so every existing caller keeps
-- binding correctly.
CREATE PROCEDURE admin.usp_Ban_Create @AccountId INT NULL,
                                      @CharacterId INT NULL,
                                      @Reason TINYINT,
                                      @ExpiresAtUtc DATETIME2(3) = NULL,
                                      @ActorAccountId INT = NULL,
                                      @ActorCharacterId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @AccountId IS NULL AND @CharacterId IS NULL
        THROW 50301, N'A ban must target at least one of @AccountId or @CharacterId.', 1;

    INSERT INTO admin.Bans (AccountId, CharacterId, Reason, ExpiresAtUtc, ActorAccountId, ActorCharacterId)
    OUTPUT INSERTED.BanId
    VALUES (@AccountId, @CharacterId, @Reason, @ExpiresAtUtc, @ActorAccountId, @ActorCharacterId);
END;
