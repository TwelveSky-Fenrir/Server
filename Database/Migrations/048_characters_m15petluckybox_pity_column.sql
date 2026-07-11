-- Migrations/048_characters_m15petluckybox_pity_column.sql
-- Adds the persisted M15 Pet Lucky Box (world.Items 8111) pity counter (legacy gBox8111,
-- Server/Header/Protocol/STRUCT.h:568-571), closing the confirmation-pass gap that left
-- PlayerRuntimeState.M15PetLuckyBoxPity session-scoped only (reset to 0 on every relog/zone transfer) -- see
-- that field's own remarks and LootBoxUseItemHandler's own remarks for the full citation trail.
--
-- Ceiling 200 is not a guess: it is already a named constant in code
-- (Application/Fenrir.Application.Game.Domain/World/Loot/M15PetLuckyBox8111RewardTable.PityCeiling), itself
-- sourced from Server/ts25zone/S04_MyWork03.cpp:1615-1621,7729-7735.
--
-- Standalone additive migration -- never edit the already-applied game.Characters creation script
-- (Tables/game/Characters.sql) per the DbMigrator SHA-256 journal rule. Idempotent so a re-run no-ops. Same
-- pattern as Migrations/041 (WarPoint) and Migrations/047 (PetBagDate).
IF COL_LENGTH('game.Characters', 'M15PetLuckyBoxPity') IS NULL
    ALTER TABLE game.Characters
        ADD M15PetLuckyBoxPity INT NOT NULL
            CONSTRAINT DF_Characters_M15PetLuckyBoxPity DEFAULT 0
            CONSTRAINT CK_Characters_M15PetLuckyBoxPity CHECK (M15PetLuckyBoxPity BETWEEN 0 AND 200);
