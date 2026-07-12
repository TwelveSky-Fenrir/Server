-- transaction-composition-audit fix: registers 50355, thrown by usp_Character_ApplyTribeFourConversion's
-- new @ConsumeQuota=1 branch when the shared daily admin.TribeFourQuota is already exhausted -- quota
-- consumption now happens inside that procedure's own transaction instead of via a separate, uncoordinated
-- call to admin.usp_TribeFourQuota_TryConsume (see usp_Character_ApplyTribeFourConversion.sql's own header).
IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50355)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50355, 'game',
            N'usp_Character_ApplyTribeFourConversion: shared daily fourth-tribe quota exhausted.');
