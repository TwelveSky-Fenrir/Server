-- AccountId is the legacy user index -- wire tID = "MG" + decimal(AccountId). Never renumber/reseed
-- this identity column.
CREATE TABLE auth.Accounts
(
    AccountId        INT IDENTITY (1,1) NOT NULL,
    LoginName        NVARCHAR(64)       NOT NULL, -- wire buffer is 255 bytes (MAX_USER_ID_LENGTH); real IDs are far shorter
    PasswordHash     VARBINARY(32)      NOT NULL, -- Argon2id output, Fenrir.Data.Security.PasswordHasher
    PasswordSalt     VARBINARY(16)      NOT NULL,
    FailedLoginCount INT                NOT NULL
        CONSTRAINT DF_Accounts_FailedLoginCount DEFAULT 0,
    LockoutUntilUtc  DATETIME2(3)       NULL,
    IsBanned         BIT                NOT NULL
        CONSTRAINT DF_Accounts_IsBanned DEFAULT 0,
    AccountGrade     SMALLINT           NOT NULL
        CONSTRAINT DF_Accounts_AccountGrade DEFAULT 0, -- legacy uUserSort (Server/ts25login/S08_MyDB.cpp:244-245); strict "< 1 is not elevated" gate at every GM-command call site (e.g. GM-BLOCK, legacy case 519)
    CreatedAtUtc     DATETIME2(3)       NOT NULL
        CONSTRAINT DF_Accounts_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Accounts PRIMARY KEY CLUSTERED (AccountId),
    CONSTRAINT UQ_Accounts_LoginName UNIQUE (LoginName)
);
