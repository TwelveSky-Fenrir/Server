-- Records one real mouse-PIN comparison attempt (success or failure) against the account-scoped,
-- cross-reconnect FailedAttempts/LockedUntilUtc counter on auth.AccountPins. Same progressive-lockout
-- thresholds/durations as usp_Account_RecordLoginAttempt for operator-consistency between the two lockout
-- mechanisms, despite the PIN's much smaller keyspace -- each attempt already costs a full Argon2id verify
-- (Infrastructure/Fenrir.Data/Security/PasswordHasher.cs, 64 MiB/3 iterations), so the lockout's purpose
-- here is blunting automated brute forcing across reconnects, not compensating for a cheap hash.
--
-- Not idempotent when @Success = 0: each call increments FailedAttempts, so callers must report each real
-- comparison exactly once -- never call this for an outcome rejected before a PasswordHasher.Verify ever
-- ran (an already-locked account, a malformed PIN, a not-yet-configured PIN).
CREATE PROCEDURE auth.usp_AccountPin_RecordAttempt @AccountId INT,
                                                   @Success BIT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE auth.AccountPins
    SET FailedAttempts = CASE WHEN @Success = 1 THEN 0 ELSE FailedAttempts + 1 END,
        LockedUntilUtc = CASE
                             WHEN @Success = 1 THEN NULL
                             WHEN FailedAttempts + 1 >= 10 THEN DATEADD(MINUTE, 15, SYSUTCDATETIME())
                             WHEN FailedAttempts + 1 >= 5 THEN DATEADD(MINUTE, 1, SYSUTCDATETIME())
                             ELSE LockedUntilUtc
            END
    WHERE AccountId = @AccountId;
END;
