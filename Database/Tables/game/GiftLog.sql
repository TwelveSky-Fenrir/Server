CREATE TABLE game.GiftLog
(
    GiftLogId    INT IDENTITY (1,1) NOT NULL,
    AccountId    INT                NOT NULL,
    ProductId    INT                NULL, 
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
    CONSTRAINT FK_GiftLog_Auth_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId),
    INDEX IX_GiftLog_Account NONCLUSTERED (AccountId, CreatedAtUtc DESC) INCLUDE (ProductId, Quantity, Value, Status)
);
