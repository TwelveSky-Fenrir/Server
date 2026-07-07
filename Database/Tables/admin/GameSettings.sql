-- Singleton row for admin-tunable settings with no legacy source value (unlike most numeric constants,
-- which stay as verified C# consts). ProxyShopDurationDays is a Fenrir-invented substitute for legacy's
-- unported aProxyShopDate rental field.
CREATE TABLE admin.GameSettings
(
    Id                    TINYINT NOT NULL
        CONSTRAINT DF_GameSettings_Id DEFAULT 1,
    ProxyShopDurationDays TINYINT NOT NULL
        CONSTRAINT DF_GameSettings_ProxyShopDurationDays DEFAULT 7,
    CONSTRAINT PK_GameSettings PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_GameSettings_Id CHECK (Id = 1),
    CONSTRAINT CK_GameSettings_ProxyShopDurationDays CHECK (ProxyShopDurationDays > 0)
);
