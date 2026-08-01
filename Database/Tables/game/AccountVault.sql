-- Legacy: masterinfo.uSaveMoney/uSaveMoney2 (memberinfo's mirrored copy dropped as redundant), kept as
-- Money/Money2. Widened to BIGINT vs. the legacy int(11) since this pool can outgrow it.
CREATE TABLE game.AccountVault
(
    AccountId    INT          NOT NULL,
    Money        BIGINT       NOT NULL
        CONSTRAINT DF_AccountVault_Money DEFAULT 0,
    Money2       BIGINT       NOT NULL
        CONSTRAINT DF_AccountVault_Money2 DEFAULT 0,
    BigMoney     INT          NOT NULL
        CONSTRAINT DF_AccountVault_BigMoney DEFAULT 0
        CONSTRAINT CK_AccountVault_BigMoney CHECK (BigMoney >= 0), -- account-scoped Save/vault BigMoney pool backing the Inventory<->Save BigMoney transfer (CZ_PROCESS_DATA_SEND tSort 242/245), see usp_AccountVault_TransferBigMoneyWithCharacter
    UpdatedAtUtc DATETIME2(3) NOT NULL
        CONSTRAINT DF_AccountVault_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AccountVault PRIMARY KEY CLUSTERED (AccountId),
    CONSTRAINT CK_AccountVault_Money CHECK (Money >= 0),
    CONSTRAINT CK_AccountVault_Money2 CHECK (Money2 >= 0),
    -- Cross-schema FK naming: see admin.Bans' own header comment for the FK_<ChildTable>_<TargetSchema>_<Role>
    -- convention.
    CONSTRAINT FK_AccountVault_Auth_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId)
);
