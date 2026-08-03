IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50357)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50357, 'game',
            N'usp_HeroRanking_ClaimReward: reward already claimed, or no claimable ranking row for this character/period.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50358)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50358, 'game', N'usp_HeroRanking_ClaimReward: unknown character for the contribution-points grant.');
