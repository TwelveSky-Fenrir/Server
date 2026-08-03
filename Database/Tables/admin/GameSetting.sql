CREATE TABLE admin.GameSetting
(
    Id                    TINYINT NOT NULL
        CONSTRAINT DF_GameSetting_Id DEFAULT 1,
    ProxyShopDurationDays TINYINT NOT NULL
        CONSTRAINT DF_GameSetting_ProxyShopDurationDays DEFAULT 7,
    CONSTRAINT PK_GameSetting PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_GameSetting_Id CHECK (Id = 1),
    CONSTRAINT CK_GameSetting_ProxyShopDurationDays CHECK (ProxyShopDurationDays > 0)
);
