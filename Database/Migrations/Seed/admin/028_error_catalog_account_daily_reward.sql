IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50359)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50359, 'game',
            N'usp_AccountDailyReward_Claim: the supplied character does not belong to the claiming account.');

UPDATE admin.ErrorCatalog
SET Description = N'usp_AccountDailyReward_Claim: already claimed today, fully claimed, or unknown account.'
WHERE ErrorNumber = 50270;
