CREATE TABLE admin.BanAudit
(
    BanAuditId       BIGINT IDENTITY (1,1) NOT NULL,
    BanId            INT                   NOT NULL,
    CorrelationId    UNIQUEIDENTIFIER      NOT NULL,
    ActorAccountId   INT                   NOT NULL,
    ActorCharacterId INT                   NOT NULL,
    AuditPayload     NVARCHAR(512)         NOT NULL,
    CreatedAtUtc     DATETIME2(3)          NOT NULL
        CONSTRAINT DF_BanAudit_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_BanAudit PRIMARY KEY CLUSTERED (BanAuditId),
    CONSTRAINT CK_BanAudit_Payload CHECK (LEN(LTRIM(RTRIM(AuditPayload))) > 0),
    CONSTRAINT FK_BanAudit_Ban FOREIGN KEY (BanId) REFERENCES admin.Bans (BanId),
    CONSTRAINT FK_BanAudit_ActorAccount FOREIGN KEY (ActorAccountId) REFERENCES auth.Accounts (AccountId),
    CONSTRAINT FK_BanAudit_ActorCharacter FOREIGN KEY (ActorCharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT UQ_BanAudit_BanId UNIQUE NONCLUSTERED (BanId),
    CONSTRAINT UQ_BanAudit_CorrelationId UNIQUE NONCLUSTERED (CorrelationId),
    INDEX IX_BanAudit_ActorCharacter_CreatedAtUtc NONCLUSTERED (ActorCharacterId, CreatedAtUtc) INCLUDE (BanId)
);
