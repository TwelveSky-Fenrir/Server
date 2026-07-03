-- Chat mute ledger (legacy MemberInfo.uMuteState, checked by playuser ZPP 45 with a SELECT per chat
-- message -- report 06 §1.7). Modeled on admin.Bans (same target model, same reasoning): account-level
-- OR character-level target, both NULL-able with the CHECK as the "at least one target" guard;
-- ExpiresAtUtc NULL = permanent; Reason is a store-as-given TINYINT category for future GM tooling.
-- LiftedAtUtc is the one column Bans does not have: a mute is routinely lifted early by a GM
-- (usp_Mute_Lift), and lifting must not destroy the audit row -- NULL = still in force (subject to
-- expiry). Fenrir does NOT re-query this table per message like the legacy did: the active mute is
-- loaded once at world entry (usp_Mute_GetActiveForCharacter) into PlayerRuntimeState as a hidden flag.
-- Not memory-optimized for the same FK reason as admin.Bans.
CREATE TABLE admin.Mutes
(
    MuteId       INT IDENTITY(1,1) NOT NULL,
    AccountId    INT NULL,
    CharacterId  INT NULL,
    Reason       TINYINT NOT NULL,
    ExpiresAtUtc DATETIME2(3) NULL,
    LiftedAtUtc  DATETIME2(3) NULL,
    CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Mutes_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Mutes PRIMARY KEY CLUSTERED (MuteId),
    CONSTRAINT CK_Mutes_AccountOrCharacter CHECK (AccountId IS NOT NULL OR CharacterId IS NOT NULL),
    CONSTRAINT FK_Mutes_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    CONSTRAINT FK_Mutes_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    INDEX        IX_Mutes_Account NONCLUSTERED (AccountId) INCLUDE (ExpiresAtUtc, LiftedAtUtc, Reason),
    INDEX        IX_Mutes_Character NONCLUSTERED (CharacterId) INCLUDE (ExpiresAtUtc, LiftedAtUtc, Reason)
);
