-- Legacy `giftinfo`: a delivery queue -- a pending row is claimable in-game, then flipped to Claimed
-- (Status: 0=Pending, 1=Claimed).
-- ProductId (legacy iIndex) is deliberately an unenforced plain INT, not a FK: GIFT_INFO_V2 comments this
-- field as "item id", so it may reference world.Items rather than world.ItemMallProducts -- a hard FK to
-- the wrong table would silently reject legitimate rows.
CREATE TABLE game.Gifts
(
    GiftId       INT IDENTITY (1,1) NOT NULL,
    AccountId    INT                NOT NULL,
    ProductId    INT                NULL,
    Quantity     INT                NOT NULL
        CONSTRAINT DF_Gifts_Quantity DEFAULT 0,
    Value        INT                NOT NULL
        CONSTRAINT DF_Gifts_Value DEFAULT 0,
    Status       TINYINT            NOT NULL
        CONSTRAINT DF_Gifts_Status DEFAULT 0,
    CreatedAtUtc DATETIME2(3)       NOT NULL
        CONSTRAINT DF_Gifts_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Gifts PRIMARY KEY CLUSTERED (GiftId),
    CONSTRAINT CK_Gifts_Status CHECK (Status IN (0, 1)),
    CONSTRAINT FK_Gifts_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    INDEX IX_Gifts_Account_Status NONCLUSTERED (AccountId, Status)
);
