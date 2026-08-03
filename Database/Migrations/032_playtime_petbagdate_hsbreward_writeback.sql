
IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'PlayTime1')
ALTER TABLE game.Characters
    ADD PlayTime1 INT NOT NULL
        CONSTRAINT DF_Characters_PlayTime1 DEFAULT 0
        CONSTRAINT CK_Characters_PlayTime1 CHECK (PlayTime1 >= 0); 
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'PlayTime3')
ALTER TABLE game.Characters
    ADD PlayTime3 INT NOT NULL
        CONSTRAINT DF_Characters_PlayTime3 DEFAULT 0
        CONSTRAINT CK_Characters_PlayTime3 CHECK (PlayTime3 >= 0); 
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'HsbStoneRewardClaimed')
ALTER TABLE game.Characters
    ADD HsbStoneRewardClaimed INT NOT NULL
        CONSTRAINT DF_Characters_HsbStoneRewardClaimed DEFAULT -1
        CONSTRAINT CK_Characters_HsbStoneRewardClaimed CHECK (HsbStoneRewardClaimed BETWEEN -1 AND 1); 
GO
