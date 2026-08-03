IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50360)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50360,
            'game',
            N'usp_Cash_Credit / usp_Cash_CreditAndConsumeItem: crediting this account''s cash balance would exceed the legacy cash cap (2,000,000,000).');
