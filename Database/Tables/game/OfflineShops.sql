CREATE TABLE game.OfflineShops
(
    CharacterId INT          NOT NULL,
    ZoneNumber  SMALLINT     NULL,
    ShopState   TINYINT      NOT NULL
        CONSTRAINT DF_OfflineShops_ShopState DEFAULT 0,
    ShopDate    INT          NOT NULL
        CONSTRAINT DF_OfflineShops_ShopDate DEFAULT 0,
    Money       INT          NOT NULL
        CONSTRAINT DF_OfflineShops_Money DEFAULT 0,
    BigMoney    INT          NOT NULL
        CONSTRAINT DF_OfflineShops_BigMoney DEFAULT 0,
    LocationX   INT          NOT NULL
        CONSTRAINT DF_OfflineShops_LocationX DEFAULT 0,
    LocationY   INT          NOT NULL
        CONSTRAINT DF_OfflineShops_LocationY DEFAULT 0,
    LocationZ   INT          NOT NULL
        CONSTRAINT DF_OfflineShops_LocationZ DEFAULT 0,
    ShopName    NVARCHAR(48) NOT NULL
        CONSTRAINT DF_OfflineShops_ShopName DEFAULT N'',
    CONSTRAINT PK_OfflineShops PRIMARY KEY CLUSTERED (CharacterId),
    CONSTRAINT FK_OfflineShops_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT FK_OfflineShops_World_Zone FOREIGN KEY (ZoneNumber) REFERENCES world.Zones (ZoneNumber),
    INDEX IX_OfflineShops_Zone NONCLUSTERED (ZoneNumber)
);
