-- Historical/active ban ledger (legacy `baduserlist`: rowID, aInsertTime, tSort, tBanUntilDate, uUserIdx,
-- uCharIdx). A richer, queryable replacement for legacy's flat MyISAM table -- NOT a duplicate of
-- auth.Accounts.IsBanned (a fast boolean flag usp_Account_Authenticate already returns); this table is
-- the ban *log* (who, why, until when, account-level or character-level) that something has to set
-- IsBanned from, and is also what a per-character ban check (world entry, not just login) needs.
-- AccountId/CharacterId are both NULL-able: legacy could ban by uUserIdx (account) or uCharIdx
-- (character) independently (uUserIdx/uCharIdx both default 0 in the dump, i.e. either one could be
-- "unset") -- the CHECK below is the normalized equivalent of "at least one target must be real" (a
-- literal 0 is translated to NULL at import time, per the cross-domain 0-is-not-a-valid-row convention).
-- Reason is the legacy tSort column, stored as a plain TINYINT category code -- the dump has 0 populated
-- rows for baduserlist, so its enumeration was never reverse-engineered from real data; store as-given,
-- define Fenrir's own enum in C# once GM tooling needs to assign a reason.
-- ExpiresAtUtc NULL = permanent ban. Legacy tBanUntilDate is a raw Unix-epoch INT defaulting to 0; 0 is
-- not "1970-01-01 UTC" in any legacy game sense, it is the sentinel for "no expiry" (the same
-- 0-is-absent idiom used across every other legacy domain) -- so any future import of real legacy ban
-- rows must map tBanUntilDate = 0 -> NULL and tBanUntilDate <> 0 -> DATEADD(SECOND, tBanUntilDate,
-- '1970-01-01').
-- Deliberately NOT memory-optimized despite being read on the login/world-entry path: it needs a real
-- FOREIGN KEY to auth.Accounts and game.Characters, and In-Memory OLTP only supports FOREIGN KEY
-- constraints that reference another memory-optimized table's primary key (both of those are, correctly,
-- ordinary disk-based tables) -- so a plain table + plain procs is not just "good enough for M1", it is
-- the only option that keeps referential integrity intact. Contrast with admin.BlockedIps, which has no
-- FK at all and so is free to be memory-optimized + natively compiled.
CREATE TABLE admin.Bans
(
    BanId        INT IDENTITY(1,1) NOT NULL,
    AccountId    INT NULL,
    CharacterId  INT NULL,
    Reason       TINYINT NOT NULL,
    ExpiresAtUtc DATETIME2(3) NULL,
    CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Bans_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Bans PRIMARY KEY CLUSTERED (BanId),
    CONSTRAINT CK_Bans_AccountOrCharacter CHECK (AccountId IS NOT NULL OR CharacterId IS NOT NULL),
    CONSTRAINT FK_Bans_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    CONSTRAINT FK_Bans_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    INDEX        IX_Bans_Account NONCLUSTERED (AccountId) INCLUDE (ExpiresAtUtc, Reason),
    INDEX        IX_Bans_Character NONCLUSTERED (CharacterId) INCLUDE (ExpiresAtUtc, Reason)
);
