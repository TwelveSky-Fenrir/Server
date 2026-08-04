CREATE TABLE game.PvpKillCooldownLedger
(
    ClaimId             UNIQUEIDENTIFIER NOT NULL,
    AttackerCharacterId INT              NOT NULL,
    DefenderCharacterId INT              NOT NULL,
    WasAccepted         BIT              NOT NULL,
    CooldownExpiresAtUtc DATETIME2(3)    NULL,
    RecordedAtUtc       DATETIME2(3)     NOT NULL
        CONSTRAINT DF_PvpKillCooldownLedger_RecordedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_PvpKillCooldownLedger PRIMARY KEY CLUSTERED (ClaimId),
    CONSTRAINT CK_PvpKillCooldownLedger_ClaimId CHECK
        (ClaimId <> '00000000-0000-0000-0000-000000000000'),
    CONSTRAINT CK_PvpKillCooldownLedger_AttackerCharacterId CHECK (AttackerCharacterId > 0),
    CONSTRAINT CK_PvpKillCooldownLedger_DefenderCharacterId CHECK (DefenderCharacterId > 0),
    CONSTRAINT CK_PvpKillCooldownLedger_DistinctCharacters CHECK (AttackerCharacterId <> DefenderCharacterId),
    CONSTRAINT CK_PvpKillCooldownLedger_AcceptedExpiry CHECK
        ((WasAccepted = 1 AND CooldownExpiresAtUtc IS NOT NULL) OR
         (WasAccepted = 0 AND CooldownExpiresAtUtc IS NULL)),
    INDEX IX_PvpKillCooldownLedger_Pair_RecordedAtUtc NONCLUSTERED
        (AttackerCharacterId, DefenderCharacterId, RecordedAtUtc DESC)
        INCLUDE (WasAccepted, CooldownExpiresAtUtc)
);
