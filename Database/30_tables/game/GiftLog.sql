-- Append-only audit trail of every state a game.Gifts row has passed through (legacy `giftinfo_log`, an
-- exact column-for-column mirror of `giftinfo` maintained by the legacy app layer purely as history).
-- Deliberately NOT a child table of game.Gifts (no FK/cascade back to GiftId, own IDENTITY space): a
-- claimed/expired Gifts row may later be purged from the live queue while its log entries must survive, so
-- this is a free-standing copy written on every state transition (enqueue, claim), not derived data.
-- No unique constraint on (AccountId, ...): the same account legitimately accumulates many historical rows.
-- Starts empty (no existing players yet) -- schema only, no seed, per this domain's brief.
CREATE TABLE game.GiftLog
(
    GiftLogId    INT IDENTITY(1,1) NOT NULL,
    AccountId    INT     NOT NULL,
    ProductId    INT NULL, -- see game.Gifts for why this is an unenforced reference, not a real FK
    Quantity     INT     NOT NULL CONSTRAINT DF_GiftLog_Quantity DEFAULT 0,
    Value        INT     NOT NULL CONSTRAINT DF_GiftLog_Value DEFAULT 0,
    Status       TINYINT NOT NULL CONSTRAINT DF_GiftLog_Status DEFAULT 0,
    CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_GiftLog_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_GiftLog PRIMARY KEY CLUSTERED (GiftLogId),
    CONSTRAINT CK_GiftLog_Status CHECK (Status IN (0, 1)),
    CONSTRAINT FK_GiftLog_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    INDEX        IX_GiftLog_Account NONCLUSTERED (AccountId)
);
