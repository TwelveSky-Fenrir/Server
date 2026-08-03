CREATE TABLE admin.Mutes
(
    MuteId           INT IDENTITY (1,1) NOT NULL,
    AccountId        INT                NULL,
    CharacterId      INT                NULL,
    Reason           TINYINT            NOT NULL,
    ExpiresAtUtc     DATETIME2(3)       NULL,
    LiftedAtUtc      DATETIME2(3)       NULL,
    CreatedAtUtc     DATETIME2(3)       NOT NULL
        CONSTRAINT DF_Mutes_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    ActorAccountId   INT                NULL, 
    ActorCharacterId INT                NULL,
    CONSTRAINT PK_Mutes PRIMARY KEY CLUSTERED (MuteId),
    CONSTRAINT CK_Mutes_AccountOrCharacter CHECK (AccountId IS NOT NULL OR CharacterId IS NOT NULL),
    CONSTRAINT FK_Mutes_Auth_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    CONSTRAINT FK_Mutes_Game_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    INDEX IX_Mutes_Account NONCLUSTERED (AccountId) INCLUDE (CharacterId, ExpiresAtUtc, LiftedAtUtc, Reason, CreatedAtUtc),
    INDEX IX_Mutes_Character NONCLUSTERED (CharacterId) INCLUDE (AccountId, ExpiresAtUtc, LiftedAtUtc, Reason, CreatedAtUtc),
    INDEX IX_Mutes_ActorAccount NONCLUSTERED (ActorAccountId) INCLUDE (CreatedAtUtc, Reason) WHERE (ActorAccountId IS NOT NULL),
    INDEX IX_Mutes_ActorCharacter NONCLUSTERED (ActorCharacterId) INCLUDE (CreatedAtUtc, Reason) WHERE (ActorCharacterId IS NOT NULL)
);
