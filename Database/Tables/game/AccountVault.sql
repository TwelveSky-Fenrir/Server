-- Legacy: masterinfo.uSaveMoney/uSaveMoney2 (memberinfo's mirrored copy dropped as redundant), kept as
-- Money/Money2. Widened to BIGINT vs. the legacy int(11) since this pool can outgrow it.
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
