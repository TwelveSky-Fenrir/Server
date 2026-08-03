
IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'AnimalDoubleExp')
ALTER TABLE game.Characters
    ADD AnimalDoubleExp INT NOT NULL
        CONSTRAINT DF_Characters_AnimalDoubleExp DEFAULT 0
        CONSTRAINT CK_Characters_AnimalDoubleExp CHECK (AnimalDoubleExp >= 0); 
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'DmgBoost')
ALTER TABLE game.Characters
    ADD DmgBoost INT NOT NULL
        CONSTRAINT DF_Characters_DmgBoost DEFAULT 0
        CONSTRAINT CK_Characters_DmgBoost CHECK (DmgBoost >= 0); 
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'HPBoost')
ALTER TABLE game.Characters
    ADD HPBoost INT NOT NULL
        CONSTRAINT DF_Characters_HPBoost DEFAULT 0
        CONSTRAINT CK_Characters_HPBoost CHECK (HPBoost >= 0); 
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'CriBoost')
ALTER TABLE game.Characters
    ADD CriBoost INT NOT NULL
        CONSTRAINT DF_Characters_CriBoost DEFAULT 0
        CONSTRAINT CK_Characters_CriBoost CHECK (CriBoost >= 0); 
GO
