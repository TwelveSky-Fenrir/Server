-- Replaces the legacy `masterinfo`+`memberinfo` DUAL-COPY pattern (verified: masterinfo was a redundant
-- multi-tier-server mirror of an account's shared bank, not real reference data) with one clean table, no
-- mirroring. Legacy had two money pools (uSaveMoney/uSaveMoney2 on masterinfo) -- kept as Money/Money2 so
-- neither is silently dropped, even though only one of the two legacy tables' pack code is still reachable
-- in the currently active build (see game.AccountVaultItems for the direct source citation).
-- BIGINT (not INT, unlike the legacy `int(11)` columns): a shared account-level currency pool is exactly
-- the kind of value that plausibly outgrows INT range over an account's lifetime; the legacy column width
-- was never a hard wire-protocol limit the way e.g. Characters.Name's length is.
-- Regular (not memory-optimized) table: account vault changes are not a per-tick hot path the way
-- game.TribeBank deposits are, per this domain's brief.
-- Starts empty (no existing players yet) -- schema only, no seed, per this domain's brief.
CREATE TABLE game.AccountVault
(
    AccountId    INT    NOT NULL,
    Money        BIGINT NOT NULL CONSTRAINT DF_AccountVault_Money DEFAULT 0,
    Money2       BIGINT NOT NULL CONSTRAINT DF_AccountVault_Money2 DEFAULT 0,
    UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AccountVault_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AccountVault PRIMARY KEY CLUSTERED (AccountId),
    CONSTRAINT CK_AccountVault_Money CHECK (Money >= 0),
    CONSTRAINT CK_AccountVault_Money2 CHECK (Money2 >= 0),
    CONSTRAINT FK_AccountVault_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId)
);
