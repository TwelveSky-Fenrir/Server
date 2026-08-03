IF NOT EXISTS (SELECT 1
               FROM admin.GameSetting
               WHERE Id = 1)
    INSERT INTO admin.GameSetting (Id, ProxyShopDurationDays)
    VALUES (1, 7);
