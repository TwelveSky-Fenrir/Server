-- Legacy MemberInfo.uMuteState. LiftedAtUtc NULL = still in force (subject to ExpiresAtUtc); GM lift
-- (usp_Mute_Lift) sets it without deleting the audit row. Unlike legacy (re-checked per chat message),
-- Fenrir loads the active mute once at world entry into PlayerRuntimeState.
-- ActorAccountId/ActorCharacterId close a basse-severity audit asymmetry against admin.Bans (which already
-- tracked the issuing GM): same shape as Bans' pair -- independently nullable, no FK (must survive deletion
-- of the actor's own account/character), populated only when a GM issues the mute via usp_Mute_Create's
-- trailing optional parameters; a system-imposed/legacy-import mute legitimately has neither.
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
    ActorAccountId   INT                NULL, -- the GM who issued this mute (independently nullable like the target columns -- a system-imposed/legacy-import mute legitimately has no GM actor); deliberately NO FK, must survive deletion of the actor's own account/character
    ActorCharacterId INT                NULL,
    CONSTRAINT PK_Mutes PRIMARY KEY CLUSTERED (MuteId),
    CONSTRAINT CK_Mutes_AccountOrCharacter CHECK (AccountId IS NOT NULL OR CharacterId IS NOT NULL),
    -- Cross-schema FK naming: see admin.Bans' own header comment for the FK_<ChildTable>_<TargetSchema>_<Role>
    -- convention this pair follows.
    CONSTRAINT FK_Mutes_Auth_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    CONSTRAINT FK_Mutes_Game_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    -- Covers usp_Mute_GetActiveForCharacter's full projection when the account-side leg of its OR wins the seek.
    INDEX IX_Mutes_Account NONCLUSTERED (AccountId) INCLUDE (CharacterId, ExpiresAtUtc, LiftedAtUtc, Reason, CreatedAtUtc),
    -- Covers usp_Mute_GetActiveForCharacter's full projection when the character-side leg wins; CharacterId
    -- alone (no INCLUDE needed) already fully covers the batched usp_Mute_GetActiveForCharacters sibling.
    INDEX IX_Mutes_Character NONCLUSTERED (CharacterId) INCLUDE (AccountId, ExpiresAtUtc, LiftedAtUtc, Reason, CreatedAtUtc),
    -- Mirrors admin.Bans' ActorAccountId/ActorCharacterId indexes exactly: filtered because the vast
    -- majority of mutes have no GM actor, same INCLUDE shape (CreatedAtUtc, Reason) for a future GM-audit
    -- read path over these two columns.
    INDEX IX_Mutes_ActorAccount NONCLUSTERED (ActorAccountId) INCLUDE (CreatedAtUtc, Reason) WHERE (ActorAccountId IS NOT NULL),
    INDEX IX_Mutes_ActorCharacter NONCLUSTERED (ActorCharacterId) INCLUDE (CreatedAtUtc, Reason) WHERE (ActorCharacterId IS NOT NULL)
);
