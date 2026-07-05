-- Legacy `baduserlist`. This is the ban log (source for auth.Accounts.IsBanned), not a duplicate of it;
-- AccountId/CharacterId nullable per independent uUserIdx/uCharIdx targeting (legacy 0 = unset -> NULL).
-- ExpiresAtUtc NULL = permanent; legacy tBanUntilDate epoch 0 must import as NULL, nonzero as
-- DATEADD(SECOND, tBanUntilDate, '1970-01-01').
CREATE TABLE admin.Bans
(
    BanId        INT IDENTITY(1,1) NOT NULL,
    AccountId    INT NULL,
    CharacterId  INT NULL,
    Reason       TINYINT NOT NULL,
    ExpiresAtUtc DATETIME2(3) NULL,
    CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Bans_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Bans PRIMARY KEY CLUSTERED (BanId),
    CONSTRAINT CK_Bans_AccountOrCharacter CHECK (AccountId IS NOT NULL OR CharacterId IS NOT NULL),
    CONSTRAINT FK_Bans_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    CONSTRAINT FK_Bans_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    INDEX        IX_Bans_Account NONCLUSTERED (AccountId) INCLUDE (ExpiresAtUtc, Reason),
    INDEX        IX_Bans_Character NONCLUSTERED (CharacterId) INCLUDE (ExpiresAtUtc, Reason)
);
