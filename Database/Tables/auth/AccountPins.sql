CREATE TABLE auth.AccountPins
(
    AccountId        INT           NOT NULL,
    PinHash          VARBINARY(32) NOT NULL,
    PinSalt          VARBINARY(16) NOT NULL,
    UpdatedAtUtc     DATETIME2(3)  NOT NULL
        CONSTRAINT DF_AccountPins_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    FailedLoginCount INT           NOT NULL
        CONSTRAINT DF_AccountPins_FailedLoginCount DEFAULT 0,
    LockoutUntilUtc  DATETIME2(3)  NULL,
    CONSTRAINT PK_AccountPins PRIMARY KEY CLUSTERED (AccountId),
    CONSTRAINT FK_AccountPins_Accounts FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    CONSTRAINT CK_AccountPins_FailedLoginCount CHECK (FailedLoginCount >= 0)
);
