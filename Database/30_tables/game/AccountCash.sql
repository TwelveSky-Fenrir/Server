-- Real-money cash balance, ACCOUNT-scoped (legacy MemberInfo.uCash, written by ts25extra's cash-shop v1
-- opcodes 14/15 -- report 06 §3.2: no Cash column existed anywhere in Fenrir before this table). One row
-- per account that ever received cash; a missing row reads as balance 0 (usp_Cash_GetBalance) and as
-- insufficient funds (usp_Cash_Debit) -- same missing-row philosophy as game.AccountVault.
-- CHECK (Balance >= 0) is the reactivated overdraft guard: the legacy cash-shop v2 (opcode 30,
-- tCashPiece) shipped with the balance check COMMENTED OUT (purchase without debit, report 06 §3.3);
-- decision D9 reactivates the guard and usp_Cash_Debit is the ONLY debit path, with this constraint as
-- the last-resort backstop under any future bug.
-- Deliberately NOT memory-optimized: needs the real FK to auth.Accounts (disk-based), and cash moves at
-- human purchase frequency, not tick frequency -- same reasoning as admin.Bans.
CREATE TABLE game.AccountCash
(
    AccountId    INT NOT NULL,
    Balance      INT NOT NULL CONSTRAINT DF_AccountCash_Balance DEFAULT 0,
    UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AccountCash_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AccountCash PRIMARY KEY CLUSTERED (AccountId),
    CONSTRAINT CK_AccountCash_Balance CHECK (Balance >= 0),
    CONSTRAINT FK_AccountCash_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId)
);
