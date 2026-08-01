-- Singleton row for admin-tunable settings with no legacy source value (unlike most numeric constants,
-- which stay as verified C# consts). ProxyShopDurationDays is a Fenrir-invented substitute for legacy's
-- unported aProxyShopDate rental field.
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
