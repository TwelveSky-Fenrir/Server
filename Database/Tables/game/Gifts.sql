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
    CONSTRAINT FK_Gifts_Auth_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    INDEX IX_Gifts_Account_Status NONCLUSTERED (AccountId, Status)
);
