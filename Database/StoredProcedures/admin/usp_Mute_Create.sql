-- NULL @ExpiresAtUtc = permanent mute (until lifted). Not idempotent: every call inserts a new row,
-- even a repeat mute of the same target -- mutes are a log, not a single-row-per-target flag.
-- THROW 50306 if neither @AccountId nor @CharacterId is given (CK_Mutes_AccountOrCharacter is the
-- last-resort backstop under a race).
--
-- @ActorAccountId/@ActorCharacterId attribute the mute to the GM who issued it (independently nullable, same
-- shape as admin.usp_Ban_Create's identically-named parameters -- a system-imposed/legacy-import mute
-- legitimately has no GM actor). Appended as trailing optional parameters (default NULL) so every existing
-- caller keeps binding correctly.
CREATE PROCEDURE admin.usp_Mute_Create @AccountId INT NULL,
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
        THROW 50306, N'A mute must target at least one of @AccountId or @CharacterId.', 1;

    INSERT INTO admin.Mutes (AccountId, CharacterId, Reason, ExpiresAtUtc, ActorAccountId, ActorCharacterId)
    OUTPUT INSERTED.MuteId
    VALUES (@AccountId, @CharacterId, @Reason, @ExpiresAtUtc, @ActorAccountId, @ActorCharacterId);
END;
