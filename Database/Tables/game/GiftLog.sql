-- Legacy: `giftinfo_log`, a column-for-column mirror of `giftinfo` kept purely as history. Deliberately
-- not a child table of game.Gifts (no FK back to GiftId): a purged Gifts row must not take its log with it.
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
    CONSTRAINT CK_GiftLog_Status CHECK (Status IN (0, 1)),
    CONSTRAINT FK_GiftLog_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    INDEX IX_GiftLog_Account NONCLUSTERED (AccountId)
);
