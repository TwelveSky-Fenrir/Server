-- auth-hardening pass (2026-07-12): registers 50101/50102 for auth.usp_Account_Create /
-- auth.usp_Account_SetGrade, which both shipped with a THROW but never had a matching admin.ErrorCatalog
-- row -- confirmed via grep sweep of Database/Migrations/Seed/admin/**, no prior row for either number
-- existed anywhere. Both procedures and their THROW sites are unchanged by this file; it only closes the
-- catalog gap. New file rather than an edit to an earlier error-catalog script: applied scripts are never
-- edited.
IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50101)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50101, 'auth', N'usp_Account_Create: account login name already taken.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50102)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50102, 'auth', N'usp_Account_SetGrade: account login name not found.');
