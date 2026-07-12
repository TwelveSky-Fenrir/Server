-- Legacy: `giftinfo_log`, a column-for-column mirror of `giftinfo` kept purely as history. Deliberately
-- not a child table of game.Gifts (no FK back to GiftId): a purged Gifts row must not take its log with it.
-- See game.Gifts's own header for why this pair models ts25login's dead `GIFT_V2` branch rather than the
-- live fixed-10-slot `uGiftInfo` mechanism that actually shipped -- confirmed deliberate, not a citation error.
--
-- Unlike game.CashLog, no game.EventLog row is ever written alongside this table today -- this log is the
-- sole audit trail for gift movements, a deliberate scope boundary (see game.EventLog's own header for the
-- cross-log design decision), not an oversight discovered later. If gift movements ever need
-- general-admin-activity-feed visibility, follow usp_Cash_DebitAndGrantItem's optional-nested-EXEC pattern
-- rather than inventing a new one.
CREATE TABLE game.GiftLog
(
    GiftLogId    INT IDENTITY (1,1) NOT NULL,
    AccountId    INT                NOT NULL,
    ProductId    INT                NULL, -- see game.Gifts for why this is an unenforced reference, not a real FK
    Quantity     INT                NOT NULL
        CONSTRAINT DF_GiftLog_Quantity DEFAULT 0,
    Value        INT                NOT NULL
        CONSTRAINT DF_GiftLog_Value DEFAULT 0,
    Status       TINYINT            NOT NULL
        CONSTRAINT DF_GiftLog_Status DEFAULT 0,
    CreatedAtUtc DATETIME2(3)       NOT NULL
        CONSTRAINT DF_GiftLog_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_GiftLog PRIMARY KEY CLUSTERED (GiftLogId),
    -- Mirrors game.Gifts.CK_Gifts_Status; see that table's own comment for why this stays an independent
    -- CHECK rather than a shared enum lookup table.
    CONSTRAINT CK_GiftLog_Status CHECK (Status IN (0, 1)),
    -- Cross-schema FK naming: see admin.Bans' own header comment for the FK_<ChildTable>_<TargetSchema>_<Role>
    -- convention.
    CONSTRAINT FK_GiftLog_Auth_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    -- Covers usp_GiftLog_GetByAccount's SELECT/ORDER BY exactly: CreatedAtUtc DESC in the key satisfies
    -- the sort, INCLUDE carries every remaining projected column so the seek never key-lookups back to
    -- the clustered index (GiftLogId is already present for free via the clustering key).
    INDEX IX_GiftLog_Account NONCLUSTERED (AccountId, CreatedAtUtc DESC) INCLUDE (ProductId, Quantity, Value, Status)
);
