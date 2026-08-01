-- Guarded per-row for the same shared-table reason as 001_error_catalog_admin.sql.
IF
    NOT EXISTS (SELECT 1
                FROM admin.ErrorCatalog
                WHERE ErrorNumber = 50312)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50312, 'game',
            N'usp_Character_GrantTribeTransferPermit: unknown character or insufficient TribeTransferPermitCount balance for this adjustment.');
