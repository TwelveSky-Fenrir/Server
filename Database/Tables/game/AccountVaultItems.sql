CREATE TABLE game.AccountVaultItems
(
    AccountId    INT          NOT NULL,
    SlotIndex    SMALLINT     NOT NULL,
    ItemId       INT          NULL,
    Quantity     INT          NOT NULL
        CONSTRAINT DF_AccountVaultItems_Quantity DEFAULT 0,
    Value        INT          NOT NULL
        CONSTRAINT DF_AccountVaultItems_Value DEFAULT 0,
    SerialNumber INT          NOT NULL
        CONSTRAINT DF_AccountVaultItems_SerialNumber DEFAULT 0,
    SocketData   NVARCHAR(50) NULL,
    SocketGem1   INT          NOT NULL
        CONSTRAINT DF_AccountVaultItems_SocketGem1 DEFAULT 0,
    SocketGem2   INT          NOT NULL
        CONSTRAINT DF_AccountVaultItems_SocketGem2 DEFAULT 0,
    SocketGem3   INT          NOT NULL
        CONSTRAINT DF_AccountVaultItems_SocketGem3 DEFAULT 0,
    ExpireDate   INT          NOT NULL
        CONSTRAINT DF_AccountVaultItems_ExpireDate DEFAULT 0,
    CONSTRAINT PK_AccountVaultItems PRIMARY KEY CLUSTERED (AccountId, SlotIndex),
    CONSTRAINT CK_AccountVaultItems_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 27),
    CONSTRAINT CK_AccountVaultItems_Quantity CHECK (Quantity BETWEEN 1 AND 999),
    CONSTRAINT FK_AccountVaultItems_Vault FOREIGN KEY (AccountId) REFERENCES game.AccountVault (AccountId) ON DELETE CASCADE,
    CONSTRAINT FK_AccountVaultItems_World_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
