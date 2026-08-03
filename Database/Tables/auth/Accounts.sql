CREATE TABLE auth.Accounts
(
    AccountId        INT IDENTITY (1,1) NOT NULL,
    LoginName        NVARCHAR(64)       NOT NULL,
    PasswordHash     VARBINARY(32)      NOT NULL,
    PasswordSalt     VARBINARY(16)      NOT NULL,
    FailedLoginCount INT                NOT NULL
        CONSTRAINT DF_Accounts_FailedLoginCount DEFAULT 0,
    LockoutUntilUtc  DATETIME2(3)       NULL,
    IsBanned         BIT                NOT NULL
        CONSTRAINT DF_Accounts_IsBanned DEFAULT 0,
    AccountGrade     SMALLINT           NOT NULL
        CONSTRAINT DF_Accounts_AccountGrade DEFAULT 0,
    CreatedAtUtc     DATETIME2(3)       NOT NULL
        CONSTRAINT DF_Accounts_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Accounts PRIMARY KEY CLUSTERED (AccountId),
    CONSTRAINT CK_Accounts_FailedLoginCount CHECK (FailedLoginCount >= 0),
    INDEX UQ_Accounts_LoginName UNIQUE NONCLUSTERED (LoginName) INCLUDE (PasswordHash, PasswordSalt, FailedLoginCount, LockoutUntilUtc, IsBanned, AccountGrade)
);
