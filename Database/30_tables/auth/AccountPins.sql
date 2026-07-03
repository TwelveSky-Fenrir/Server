-- Legacy mouse PIN (MemberInfo.uMousePassword, MAX_MOUSE_PASSWORD_LENGTH=5: 4 ASCII digits), mandatory in prod
-- EU33 (P2ndPassword=1). Stored HASHED (Fenrir.Domain.Security.PasswordHasher, Argon2id) -- the legacy kept and
-- even UDP-logged it in clear; Fenrir never persists a clear PIN (plan decision D10). The clear-text echo the
-- client expects in LC_CREATE/CHANGE_MOUSE_PASSWORD_RECV comes from the REQUEST being answered, never from here.
-- 1:0..1 with auth.Accounts: absence of a row = "no PIN yet" (drives LC_LOGIN_RECV.tMousePassword "" vs "****").
CREATE TABLE auth.AccountPins
(
    AccountId    INT NOT NULL,
    PinHash      VARBINARY(32) NOT NULL, -- Argon2id output, same shape as auth.Accounts.PasswordHash
    PinSalt      VARBINARY(16) NOT NULL,
    UpdatedAtUtc DATETIME2(3)  NOT NULL CONSTRAINT DF_AccountPins_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AccountPins PRIMARY KEY CLUSTERED (AccountId),
    CONSTRAINT FK_AccountPins_Accounts FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId)
);
