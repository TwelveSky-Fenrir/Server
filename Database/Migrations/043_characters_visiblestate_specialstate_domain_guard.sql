-- Domain guard for game.Characters.VisibleState/SpecialState.
--
-- Both columns already exist as TINYINT NOT NULL (Tables/game/Characters.sql:188-191,
-- DF_Characters_VisibleState DEFAULT 1, DF_Characters_SpecialState DEFAULT 0) but carry no CHECK.
-- Verified against every current C# writer before adding one:
--   VisibleState -- only GmBasicCommandService.HandleVisibilityAsync (HiddenVisibleState=0,
--     ShownVisibleState=1) mutates it after creation; CreateAvatarService starts every character at 1.
--   SpecialState -- only GmBasicCommandService.HandleSelfSpecialStateAsync (EquipSpecialState=1,
--     UnequipSpecialState=0) and HandleTargetSpecialStateAsync (NchatSpecialState=2,
--     YchatSpecialState=0) mutate it after creation; CreateAvatarService starts every character at 0.
-- 0/1/2 matches the legacy domain documented in Migrations/006_avatar_state_flags_writeback.sql:60-62
-- (aSpecialState also stores 2 on a GM target, Server/ts25zone/S04_MyWork04.cpp:1431-1432,1460-1461),
-- which is why that column was deliberately widened to TINYINT/INT instead of BIT.

IF NOT EXISTS (SELECT 1
               FROM sys.check_constraints
               WHERE name = N'CK_Characters_VisibleState_Domain')
ALTER TABLE game.Characters WITH CHECK
    ADD CONSTRAINT CK_Characters_VisibleState_Domain CHECK (VisibleState IN (0, 1));
GO

IF NOT EXISTS (SELECT 1
               FROM sys.check_constraints
               WHERE name = N'CK_Characters_SpecialState_Domain')
ALTER TABLE game.Characters WITH CHECK
    ADD CONSTRAINT CK_Characters_SpecialState_Domain CHECK (SpecialState IN (0, 1, 2));
GO
