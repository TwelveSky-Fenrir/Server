
IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'WarriorPill')
ALTER TABLE game.Characters
    ADD WarriorPill INT NOT NULL
        CONSTRAINT DF_Characters_WarriorPill DEFAULT 0
        CONSTRAINT CK_Characters_WarriorPill CHECK (WarriorPill >= 0); 
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'WarriorScroll')
ALTER TABLE game.Characters
    ADD WarriorScroll INT NOT NULL
        CONSTRAINT DF_Characters_WarriorScroll DEFAULT 0
        CONSTRAINT CK_Characters_WarriorScroll CHECK (WarriorScroll >= 0); 
GO
