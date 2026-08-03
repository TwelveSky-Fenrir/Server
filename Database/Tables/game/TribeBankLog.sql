CREATE TABLE game.TribeBankLog
(
    TribeBankLogId INT IDENTITY (1,1) NOT NULL,
    TribeId        TINYINT            NOT NULL,
    SlotIndex      TINYINT            NOT NULL,
    CharacterId    INT                NULL,
    Delta          INT                NOT NULL,
    BalanceAfter   INT                NOT NULL,
    CreatedAtUtc   DATETIME2(3)       NOT NULL
        CONSTRAINT DF_TribeBankLog_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TribeBankLog PRIMARY KEY CLUSTERED (TribeBankLogId),
    CONSTRAINT CK_TribeBankLog_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 49),
    CONSTRAINT FK_TribeBankLog_Tribe FOREIGN KEY (TribeId) REFERENCES game.Tribes (TribeId),
    CONSTRAINT FK_TribeBankLog_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    INDEX IX_TribeBankLog_Tribe NONCLUSTERED (TribeId),
    INDEX IX_TribeBankLog_Character NONCLUSTERED (CharacterId)
);
