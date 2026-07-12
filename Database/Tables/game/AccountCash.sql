-- Legacy: MemberInfo.uCash; missing row reads as balance 0 (usp_Cash_GetBalance).
-- CHECK (Balance >= 0) reactivates an overdraft guard the legacy cash-shop v2 shipped commented out;
-- usp_Cash_DebitAndGrantItem is the only debit path (a bare usp_Cash_Debit used to exist but had zero
-- callers -- every debit path also grants an item -- removed as dead code in the 2026-07-12 Database/
-- cleanup pass; error codes 50240/50241 it used to throw are still live, now thrown by
-- usp_Cash_DebitAndGrantItem/usp_Cash_Credit).
CREATE TABLE game.AccountCash
(
    AccountId    INT          NOT NULL,
    Balance      INT          NOT NULL
        CONSTRAINT DF_AccountCash_Balance DEFAULT 0,
    UpdatedAtUtc DATETIME2(3) NOT NULL
        CONSTRAINT DF_AccountCash_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AccountCash PRIMARY KEY CLUSTERED (AccountId),
    CONSTRAINT CK_AccountCash_Balance CHECK (Balance >= 0),
    -- Cross-schema FK naming: see admin.Bans' own header comment for the FK_<ChildTable>_<TargetSchema>_<Role>
    -- convention.
    CONSTRAINT FK_AccountCash_Auth_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId)
);
