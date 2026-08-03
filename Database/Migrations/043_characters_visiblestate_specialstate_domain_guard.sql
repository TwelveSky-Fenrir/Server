
IF NOT EXISTS (SELECT 1
               FROM sys.check_constraints
               WHERE name = N'CK_Characters_VisibleState_Domain')
ALTER TABLE game.Characters
    WITH CHECK
        ADD CONSTRAINT CK_Characters_VisibleState_Domain CHECK (VisibleState IN (0, 1));
GO

IF NOT EXISTS (SELECT 1
               FROM sys.check_constraints
               WHERE name = N'CK_Characters_SpecialState_Domain')
ALTER TABLE game.Characters
    WITH CHECK
        ADD CONSTRAINT CK_Characters_SpecialState_Domain CHECK (SpecialState IN (0, 1, 2));
GO
