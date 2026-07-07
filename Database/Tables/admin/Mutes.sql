-- Legacy MemberInfo.uMuteState. LiftedAtUtc NULL = still in force (subject to ExpiresAtUtc); GM lift
-- (usp_Mute_Lift) sets it without deleting the audit row. Unlike legacy (re-checked per chat message),
-- Fenrir loads the active mute once at world entry into PlayerRuntimeState.
CREATE TABLE admin.Mutes
(
    MuteId       INT IDENTITY (1,1) NOT NULL,
    AccountId    INT                NULL,
    CharacterId  INT                NULL,
    Reason       TINYINT            NOT NULL,
    ExpiresAtUtc DATETIME2(3)       NULL,
    LiftedAtUtc  DATETIME2(3)       NULL,
    CreatedAtUtc DATETIME2(3)       NOT NULL
        CONSTRAINT DF_Mutes_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Mutes PRIMARY KEY CLUSTERED (MuteId),
    CONSTRAINT CK_Mutes_AccountOrCharacter CHECK (AccountId IS NOT NULL OR CharacterId IS NOT NULL),
    CONSTRAINT FK_Mutes_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    CONSTRAINT FK_Mutes_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    INDEX IX_Mutes_Account NONCLUSTERED (AccountId) INCLUDE (ExpiresAtUtc, LiftedAtUtc, Reason),
    INDEX IX_Mutes_Character NONCLUSTERED (CharacterId) INCLUDE (ExpiresAtUtc, LiftedAtUtc, Reason)
);
