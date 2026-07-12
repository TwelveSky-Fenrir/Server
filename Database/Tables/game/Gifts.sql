-- Legacy `giftinfo`: a delivery queue -- a pending row is claimable in-game, then flipped to Claimed
-- (Status: 0=Pending, 1=Claimed).
-- ProductId (legacy iIndex) is deliberately an unenforced plain INT, not a FK: GIFT_INFO_V2 comments this
-- field as "item id", so it may reference world.Items rather than world.ItemMallProducts -- a hard FK to
-- the wrong table would silently reject legitimate rows.
-- Divergence, confirmed deliberate: GIFT_INFO_V2 above is ts25login's dead `GIFT_V2` branch, gated by a
-- `//`-commented `#define GIFT_V2` (Server/Header/Protocol/DEFINE.h:116) never uncommented anywhere under
-- Server/, and internally inconsistent with its own STRUCT.h typedef besides. What actually shipped is a
-- fixed 10-slot `int uGiftInfo[10]` per account with no Quantity/Value/timestamp ever persisted
-- (Server/ts25login/H03_MyUser.h:52, Server/ts25login/S08_MyDB.cpp:939-1004); a claim always moves the item
-- into the account's 28-slot holding pen at implicit quantity/value 0 (Server/ts25login/S04_MyWork02.cpp:1461-1470).
-- This row-per-gift-plus-audit-log model is a legitimate, separately-owned modernization kept on purpose
-- (a richer admin/support surface than a bare 10-slot array) -- it is not a citation error.
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
    -- Independent CHECK, not a shared lookup table: mirrored by game.GiftLog.CK_GiftLog_Status. A shared
    -- enum table would be over-engineering for a 2-value flag scoped to this one companion-table pair --
    -- keep both CHECKs manually in sync instead.
    CONSTRAINT CK_Gifts_Status CHECK (Status IN (0, 1)),
    -- Cross-schema FK naming: see admin.Bans' own header comment for the FK_<ChildTable>_<TargetSchema>_<Role>
    -- convention.
    CONSTRAINT FK_Gifts_Auth_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    INDEX IX_Gifts_Account_Status NONCLUSTERED (AccountId, Status)
);
