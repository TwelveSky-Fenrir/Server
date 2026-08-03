IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50364)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50364,
            'game',
            N'usp_GuildMember_Add: the guild already holds Grade * 10 members -- the per-grade cap is enforced inside the insert transaction, so two simultaneous accepts cannot both pass it.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50365)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50365,
            'game',
            N'usp_Guild_UpgradeAndDebitMoney: the guild grade moved between the caller''s read and this write -- the upgrade is guarded by Grade = @Grade - 1, so two concurrent upgrades cannot both apply.');
