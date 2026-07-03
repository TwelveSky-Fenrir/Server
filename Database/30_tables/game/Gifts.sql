-- Legacy `giftinfo` (nxtserver.sql): a delivery queue -- a pending row here is claimable in-game, then
-- flipped to Claimed. `gift_event_info` (same dump) is an older, simpler predecessor with the identical
-- id/uUserIdx/iIndex/iQuantity/iValue shape but no aStatus/date column at all -- it is a strict subset of
-- giftinfo's own shape, already covered by this table, so it is not modeled as its own table.
-- ProductId (legacy iIndex) is named/typed to loosely reference world.ItemMallProducts.ItemMallProductId
-- (designed in a parallel domain) per this pass's brief, but is deliberately left an UNENFORCED plain INT,
-- not a real FK: Protocol/STRUCT.h's own GIFT_INFO_V2 struct comments this exact field as "item id", i.e.
-- it may actually be a raw world.Items.ItemId rather than an item-mall product number -- a hard FK to the
-- wrong table would silently reject legitimate gift rows, so referential integrity here is left to the
-- application layer until that ambiguity is resolved. 0 is not a meaningful ProductId either way -- the
-- app must translate 0 -> NULL the same as every other cross-domain FK-shaped column in this schema.
-- Status: 0 = Pending (not yet claimed), 1 = Claimed. Matches legacy aStatus tinyint(1) exactly.
-- Starts empty (no existing players yet) -- schema only, no seed, per this domain's brief.
CREATE TABLE game.Gifts
(
    GiftId       INT IDENTITY(1,1) NOT NULL,
    AccountId    INT     NOT NULL,
    ProductId    INT NULL,
    Quantity     INT     NOT NULL CONSTRAINT DF_Gifts_Quantity DEFAULT 0,
    Value        INT     NOT NULL CONSTRAINT DF_Gifts_Value DEFAULT 0,
    Status       TINYINT NOT NULL CONSTRAINT DF_Gifts_Status DEFAULT 0,
    CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Gifts_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Gifts PRIMARY KEY CLUSTERED (GiftId),
    CONSTRAINT CK_Gifts_Status CHECK (Status IN (0, 1)),
    CONSTRAINT FK_Gifts_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    INDEX        IX_Gifts_Account_Status NONCLUSTERED (AccountId, Status)
);
