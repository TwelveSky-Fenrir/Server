
IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'RankPoint')
ALTER TABLE game.Characters
    ADD RankPoint INT NOT NULL
        CONSTRAINT DF_Characters_RankPoint DEFAULT 0
        CONSTRAINT CK_Characters_RankPoint CHECK (RankPoint >= 0);
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'CloakLuckyBoxPity')
ALTER TABLE game.Characters
    ADD CloakLuckyBoxPity TINYINT NOT NULL
        CONSTRAINT DF_Characters_CloakLuckyBoxPity DEFAULT 0
        CONSTRAINT CK_Characters_CloakLuckyBoxPity CHECK (CloakLuckyBoxPity BETWEEN 0 AND 100);
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'CloakVariantBoxPity')
ALTER TABLE game.Characters
    ADD CloakVariantBoxPity TINYINT NOT NULL
        CONSTRAINT DF_Characters_CloakVariantBoxPity DEFAULT 0
        CONSTRAINT CK_Characters_CloakVariantBoxPity CHECK (CloakVariantBoxPity BETWEEN 0 AND 200);
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'MountVariantBoxPity')
ALTER TABLE game.Characters
    ADD MountVariantBoxPity TINYINT NOT NULL
        CONSTRAINT DF_Characters_MountVariantBoxPity DEFAULT 0
        CONSTRAINT CK_Characters_MountVariantBoxPity CHECK (MountVariantBoxPity BETWEEN 0 AND 200);
GO
