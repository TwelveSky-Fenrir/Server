-- Legacy mouse PIN (MemberInfo.uMousePassword); stored hashed, unlike legacy's cleartext/UDP-logged PIN.
-- Absence of a row = "no PIN yet" (drives LC_LOGIN_RECV.tMousePassword "" vs "****").
CREATE TABLE auth.AccountPins
(
    AccountId    INT           NOT NULL,
    PinHash      VARBINARY(32) NOT NULL, -- Argon2id output, same shape as auth.Accounts.PasswordHash
    PinSalt      VARBINARY(16) NOT NULL,
    UpdatedAtUtc DATETIME2(3)  NOT NULL
        CONSTRAINT DF_AccountPins_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AccountPins PRIMARY KEY CLUSTERED (AccountId),
    CONSTRAINT FK_AccountPins_Accounts FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId)
);
