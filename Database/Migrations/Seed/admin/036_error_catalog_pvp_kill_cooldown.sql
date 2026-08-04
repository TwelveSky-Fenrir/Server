IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50373)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50373,
            'game',
            N'usp_PvpKillCooldown_TryClaim: a claim identifier or one of the two distinct character identifiers is invalid.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50374)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50374,
            'game',
            N'usp_PvpKillCooldown_TryClaim: a claim identifier was replayed with different attacker or defender identifiers.');
