-- Fenrir-only addition, no legacy analog (see the pincode-second-password security audit's Major finding
-- "No account-scoped (cross-reconnect) brute-force throttle on the mouse PIN"): LoginClientSession.
-- PinFailureCount (Network/Fenrir.Network.Dispatch/Sessions/LoginClientSession.cs) is a per-connection
-- counter reset to 0 by every MarkPinRequired() call, i.e. on every fresh successful primary login -- an
-- attacker who already holds valid primary credentials (the only way to ever reach PinRequired) can
-- brute-force the 4-digit/10,000-combination PIN keyspace (Application/Fenrir.Application.Login.Domain/
-- Pins/MousePinFormat.cs) across an unbounded number of reconnects with no durable friction beyond
-- LoginIpRateLimiter's loose, IP-scoped budget (tuned to tolerate shared-NAT traffic, not to block brute
-- force -- an attacker distributing across a handful of IPs faces effectively no throttle today).
--
-- Mirrors auth.Accounts.FailedLoginCount/LockoutUntilUtc's existing progressive-lockout shape for the
-- primary password (see StoredProcedures/auth/usp_Account_RecordLoginAttempt.sql, consulted by
-- LoginService.AuthenticateConstantTimeAsync) rather than a purely in-memory per-session counter, per that
-- finding's own recommendation.
ALTER TABLE auth.AccountPins
    ADD FailedAttempts INT NOT NULL
            CONSTRAINT DF_AccountPins_FailedAttempts DEFAULT 0,
        LockedUntilUtc DATETIME2(3) NULL;
GO

-- New procedure (not a fix-forward of an existing one): records one real mouse-PIN comparison attempt
-- (success or failure) against the account-scoped, cross-reconnect counter above. Same progressive-lockout
-- thresholds/durations as usp_Account_RecordLoginAttempt for operator-consistency between the two
-- lockout mechanisms, despite the PIN's much smaller keyspace -- each attempt already costs a full
-- Argon2id verify (Infrastructure/Fenrir.Data/Security/PasswordHasher.cs, 64 MiB/3 iterations), so the
-- lockout's purpose here is blunting automated brute forcing across reconnects, not compensating for a
-- cheap hash.
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
GO
