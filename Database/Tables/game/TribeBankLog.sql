-- Append-only audit trail of every tribe-bank slot movement (deposit and withdrawal alike), on the same
-- model as game.CashLog: Delta is signed (+deposit/-withdraw) and BalanceAfter snapshots the slot's
-- post-movement balance. Written by game.usp_TribeBank_DepositFromCharacter and game.usp_TribeBank_Withdraw
-- in the same transaction as the movement itself -- there is no legacy equivalent (the legacy client process
-- never logged tribe-bank movements at all), this closes an audit/anti-fraud gap on a collective money flow.
-- Disk-based (unlike game.TribeBank itself): the log is cold-path/append-only, never the hot write path that
-- justified making TribeBank memory-optimized, so it can carry real FKs that TribeBank cannot.
CREATE TABLE game.TribeBankLog
(
    TribeBankLogId INT IDENTITY (1,1) NOT NULL,
    TribeId        TINYINT            NOT NULL,
    SlotIndex      TINYINT            NOT NULL,
    CharacterId    INT                NOT NULL,
    Delta          INT                NOT NULL,
    BalanceAfter   INT                NOT NULL,
    CreatedAtUtc   DATETIME2(3)       NOT NULL
        CONSTRAINT DF_TribeBankLog_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TribeBankLog PRIMARY KEY CLUSTERED (TribeBankLogId),
    CONSTRAINT CK_TribeBankLog_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 49),
    CONSTRAINT FK_TribeBankLog_Tribe FOREIGN KEY (TribeId) REFERENCES game.Tribes (TribeId),
    CONSTRAINT FK_TribeBankLog_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    INDEX IX_TribeBankLog_Tribe NONCLUSTERED (TribeId)
);
