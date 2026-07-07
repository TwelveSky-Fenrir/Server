-- Guarded per-row (not a table-empty check): admin.ErrorCatalog is shared across every domain, and other
-- domains' seed scripts may run before or after this one in any order.
IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50301)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50301, N'admin', N'usp_Ban_Create: a ban must target at least one of @AccountId or @CharacterId.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50302)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50302, N'admin', N'usp_BlockedIp_Add: IP address is already blocked.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50303)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50303, N'admin', N'usp_FirewallRule_Add: a firewall rule already exists for this IP address.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50304)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50304, N'admin', N'usp_GmAllowlist_Add: IP address is already on the GM allowlist.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50305)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50305, N'admin',
            N'usp_MacRestriction_Add: a restriction already exists for this MAC address / machine GUID pair.');
