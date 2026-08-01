-- Append-only audit trail of every tribe-bank slot movement (deposit and withdrawal alike), on the same
-- model as game.CashLog: Delta is signed (+deposit/-withdraw) and BalanceAfter snapshots the slot's
-- post-movement balance. Written by game.usp_TribeBank_DepositFromCharacter and game.usp_TribeBank_Withdraw
-- in the same transaction as the movement itself -- there is no legacy equivalent (the legacy client process
-- never logged tribe-bank movements at all), this closes an audit/anti-fraud gap on a collective money flow.
-- Disk-based (unlike game.TribeBank itself): the log is cold-path/append-only, never the hot write path that
-- justified making TribeBank memory-optimized, so it can carry real FKs that TribeBank cannot.
--
-- CharacterId is NULL-able (unlike the other columns) specifically so game.usp_Character_Delete can preserve
-- this row when the acting character is deleted: the tribe's collective-fund movement (TribeId/SlotIndex/
-- Delta/BalanceAfter/CreatedAtUtc) is the part of this audit trail that must survive per this schema's
-- permanent-audit-log convention (see admin.Bans/admin.Mutes for the same nullable-actor-column shape); the
-- specific character attribution is the only part that is allowed to be lost. Unlike Bans/Mutes there is no
-- AccountId fallback column here -- CharacterId is this table's only actor reference -- so the null-out is
-- unconditional rather than branching on an account-level record still existing.
--
-- Unlike game.CashLog, no game.EventLog row is ever written alongside this table today -- this log is the
-- sole audit trail for tribe-bank movements, a deliberate scope boundary (see game.EventLog's own header for
-- the cross-log design decision), not an oversight discovered later. If tribe-bank movements ever need
-- general-admin-activity-feed visibility, follow usp_Cash_DebitAndGrantItem's optional-nested-EXEC pattern
-- rather than inventing a new one.
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
    -- Leads with CharacterId so usp_Character_Delete's per-character null-out UPDATE is a seek, not a full
    -- scan of this ever-growing append-only table -- mirrors the CharacterId-leading posture of CashLog/
    -- GiftLog's own AccountId indexes for the same "hot write predicate on a cold-write log table" shape.
    INDEX IX_TribeBankLog_Character NONCLUSTERED (CharacterId)
);
