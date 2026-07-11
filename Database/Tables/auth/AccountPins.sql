-- Legacy mouse PIN (MemberInfo.uMousePassword); stored hashed, unlike legacy's cleartext/UDP-logged PIN.
-- Absence of a row = "no PIN yet" (drives LC_LOGIN_RECV.tMousePassword "" vs "****").
--
-- FailedAttempts/LockedUntilUtc: Fenrir-only addition, no legacy analog (see the pincode-second-password
-- security audit's Major finding "No account-scoped (cross-reconnect) brute-force throttle on the mouse
-- PIN"): LoginClientSession.PinFailureCount (Network/Fenrir.Network.Dispatch/Sessions/LoginClientSession.cs)
-- is a per-connection counter reset to 0 by every MarkPinRequired() call, i.e. on every fresh successful
-- primary login -- an attacker who already holds valid primary credentials (the only way to ever reach
-- PinRequired) can brute-force the 4-digit/10,000-combination PIN keyspace
-- (Application/Fenrir.Application.Login.Domain/Pins/MousePinFormat.cs) across an unbounded number of
-- reconnects with no durable friction beyond LoginIpRateLimiter's loose, IP-scoped budget (tuned to
-- tolerate shared-NAT traffic, not to block brute force). These two columns mirror
-- auth.Accounts.FailedLoginCount/LockoutUntilUtc's existing progressive-lockout shape for the primary
-- password (see StoredProcedures/auth/usp_Account_RecordLoginAttempt.sql) rather than a purely in-memory
-- per-session counter, per that finding's own recommendation; usp_AccountPin_RecordAttempt maintains them.
CREATE TABLE auth.AccountPins
(
    AccountId      INT           NOT NULL,
    PinHash        VARBINARY(32) NOT NULL, -- Argon2id output, same shape as auth.Accounts.PasswordHash
    PinSalt        VARBINARY(16) NOT NULL,
    UpdatedAtUtc   DATETIME2(3)  NOT NULL
        CONSTRAINT DF_AccountPins_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    FailedAttempts INT           NOT NULL
        CONSTRAINT DF_AccountPins_FailedAttempts DEFAULT 0,
    LockedUntilUtc DATETIME2(3)  NULL,
    CONSTRAINT PK_AccountPins PRIMARY KEY CLUSTERED (AccountId),
    CONSTRAINT FK_AccountPins_Accounts FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId)
);
