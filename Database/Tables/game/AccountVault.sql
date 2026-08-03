CREATE TABLE game.AccountVault
(
    AccountId    INT          NOT NULL,
    Money        BIGINT       NOT NULL
        CONSTRAINT DF_AccountVault_Money DEFAULT 0,
    Money2       BIGINT       NOT NULL
        CONSTRAINT DF_AccountVault_Money2 DEFAULT 0,
    BigMoney     INT          NOT NULL
        CONSTRAINT DF_AccountVault_BigMoney DEFAULT 0
        CONSTRAINT CK_AccountVault_BigMoney CHECK (BigMoney >= 0), 
    UpdatedAtUtc DATETIME2(3) NOT NULL
        CONSTRAINT DF_AccountVault_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AccountVault PRIMARY KEY CLUSTERED (AccountId),
    CONSTRAINT CK_AccountVault_Money CHECK (Money >= 0),
    CONSTRAINT CK_AccountVault_Money2 CHECK (Money2 >= 0),
    CONSTRAINT FK_AccountVault_Auth_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId)
);
