CREATE TABLE admin.Bans
(
    BanId            INT IDENTITY (1,1) NOT NULL,
    AccountId        INT                NULL,
    CharacterId      INT                NULL,
    Reason           TINYINT            NOT NULL,
    ExpiresAtUtc     DATETIME2(3)       NULL,
    CorrelationId    UNIQUEIDENTIFIER   NOT NULL,
    CreatedAtUtc     DATETIME2(3)       NOT NULL
        CONSTRAINT DF_Bans_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    ActorAccountId   INT                NOT NULL,
    ActorCharacterId INT                NOT NULL,
    CONSTRAINT PK_Bans PRIMARY KEY CLUSTERED (BanId),
    CONSTRAINT CK_Bans_AccountOrCharacter CHECK (AccountId IS NOT NULL OR CharacterId IS NOT NULL),
    CONSTRAINT CK_Bans_Reason CHECK (Reason BETWEEN 1 AND 32),
    CONSTRAINT FK_Bans_Auth_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    CONSTRAINT FK_Bans_Game_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT FK_Bans_ActorAccount FOREIGN KEY (ActorAccountId) REFERENCES auth.Accounts (AccountId),
    CONSTRAINT FK_Bans_ActorCharacter FOREIGN KEY (ActorCharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT UQ_Bans_CorrelationId UNIQUE NONCLUSTERED (CorrelationId),
    INDEX IX_Bans_Account NONCLUSTERED (AccountId) INCLUDE (ExpiresAtUtc, Reason, CharacterId, CreatedAtUtc),
    INDEX IX_Bans_Character NONCLUSTERED (CharacterId) INCLUDE (ExpiresAtUtc, Reason, AccountId, CreatedAtUtc),
    INDEX IX_Bans_ActorAccount NONCLUSTERED (ActorAccountId) INCLUDE (CreatedAtUtc, Reason) WHERE (ActorAccountId IS NOT NULL),
    INDEX IX_Bans_ActorCharacter NONCLUSTERED (ActorCharacterId) INCLUDE (CreatedAtUtc, Reason) WHERE (ActorCharacterId IS NOT NULL)
);
