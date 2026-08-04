CREATE TABLE game.PvpKillCooldownState
(
    AttackerCharacterId INT              NOT NULL,
    DefenderCharacterId INT              NOT NULL,
    LastClaimId         UNIQUEIDENTIFIER NOT NULL,
    CooldownExpiresAtUtc DATETIME2(3)    NOT NULL,
    UpdatedAtUtc        DATETIME2(3)     NOT NULL
        CONSTRAINT DF_PvpKillCooldownState_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_PvpKillCooldownState PRIMARY KEY CLUSTERED (AttackerCharacterId, DefenderCharacterId),
    CONSTRAINT CK_PvpKillCooldownState_AttackerCharacterId CHECK (AttackerCharacterId > 0),
    CONSTRAINT CK_PvpKillCooldownState_DefenderCharacterId CHECK (DefenderCharacterId > 0),
    CONSTRAINT CK_PvpKillCooldownState_DistinctCharacters CHECK (AttackerCharacterId <> DefenderCharacterId),
    CONSTRAINT CK_PvpKillCooldownState_LastClaimId CHECK
        (LastClaimId <> '00000000-0000-0000-0000-000000000000'),
    INDEX IX_PvpKillCooldownState_ExpiresAtUtc NONCLUSTERED (CooldownExpiresAtUtc)
        INCLUDE (AttackerCharacterId, DefenderCharacterId)
);
