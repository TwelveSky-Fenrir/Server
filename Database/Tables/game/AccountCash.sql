CREATE TABLE game.AccountCash
(
    AccountId    INT          NOT NULL,
    Balance      INT          NOT NULL
        CONSTRAINT DF_AccountCash_Balance DEFAULT 0,
    UpdatedAtUtc DATETIME2(3) NOT NULL
        CONSTRAINT DF_AccountCash_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AccountCash PRIMARY KEY CLUSTERED (AccountId),
    CONSTRAINT CK_AccountCash_Balance CHECK (Balance >= 0),
    CONSTRAINT FK_AccountCash_Auth_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId)
);
