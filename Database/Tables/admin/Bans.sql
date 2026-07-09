-- Legacy `baduserlist`. This is the ban log (source for auth.Accounts.IsBanned), not a duplicate of it;
-- AccountId/CharacterId nullable per independent uUserIdx/uCharIdx targeting (legacy 0 = unset -> NULL).
-- ExpiresAtUtc NULL = permanent; legacy tBanUntilDate epoch 0 must import as NULL, nonzero as
-- DATEADD(SECOND, tBanUntilDate, '1970-01-01').
CREATE TABLE admin.Bans
(
    BanId            INT IDENTITY (1,1) NOT NULL,
    AccountId        INT                NULL,
    CharacterId      INT                NULL,
    Reason           TINYINT            NOT NULL,
    ExpiresAtUtc     DATETIME2(3)       NULL,
    CreatedAtUtc     DATETIME2(3)       NOT NULL
        CONSTRAINT DF_Bans_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    ActorAccountId   INT                NULL, -- the GM who issued this ban (independently nullable like the target columns -- a system-imposed/legacy-import ban legitimately has no GM actor); deliberately NO FK, must survive deletion of the actor's own account/character
    ActorCharacterId INT                NULL,
    CONSTRAINT PK_Bans PRIMARY KEY CLUSTERED (BanId),
    CONSTRAINT CK_Bans_AccountOrCharacter CHECK (AccountId IS NOT NULL OR CharacterId IS NOT NULL),
    CONSTRAINT FK_Bans_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    CONSTRAINT FK_Bans_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    INDEX IX_Bans_Account NONCLUSTERED (AccountId) INCLUDE (ExpiresAtUtc, Reason),
    INDEX IX_Bans_Character NONCLUSTERED (CharacterId) INCLUDE (ExpiresAtUtc, Reason),
    INDEX IX_Bans_ActorAccount NONCLUSTERED (ActorAccountId) INCLUDE (CreatedAtUtc, Reason) WHERE (ActorAccountId IS NOT NULL),
    INDEX IX_Bans_ActorCharacter NONCLUSTERED (ActorCharacterId) INCLUDE (CreatedAtUtc, Reason) WHERE (ActorCharacterId IS NOT NULL)
);
