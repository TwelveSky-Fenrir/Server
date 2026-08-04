IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50301)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50301, 'admin', N'usp_Ban_Create: a ban must target at least one of @AccountId or @CharacterId.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50302)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50302, 'admin', N'usp_BlockedIp_Add: IP address is already blocked.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50303)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50303, 'admin', N'usp_FirewallRule_Add: a firewall rule already exists for this IP address.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50304)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50304, 'admin', N'usp_GmAllowlist_Add: IP address is already on the GM allowlist.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50305)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50305, 'admin',
            N'usp_MacRestriction_Add: a restriction already exists for this MAC address / machine GUID pair.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50375)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50375, 'admin', N'usp_Ban_Create: ban actor provenance is missing or invalid.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50376)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50376, 'admin', N'usp_Ban_Create: ban correlation identifier is empty.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50377)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50377, 'admin', N'usp_Ban_Create: ban audit payload is missing.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50378)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50378, 'admin', N'usp_Ban_Create: ban reason is outside the supported range.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50379)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50379, 'admin', N'usp_Ban_Create: actor provenance is not a current Basic-tier GM account and character.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50380)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50380, 'admin', N'usp_Ban_Create: target character ownership does not match target account.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50381)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50381, 'admin', N'usp_Ban_Create: correlation identifier was reused with different command data.');

IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50382)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50382, 'admin', N'usp_Ban_Create: existing ban audit does not match the correlation.');
