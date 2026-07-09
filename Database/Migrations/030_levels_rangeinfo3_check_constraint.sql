-- Level static-data schema/validation gap (legacy-behavior-translator contract "Per-level combat/progression
-- static data validation on GameServer boot (world.Levels load)"): adds a storage-level CHECK constraint
-- mirroring the legacy 0-100 bound on RangeInfo3 (Server/Header/S15_MyShare.cpp:843-846,
-- Level_CheckValidElement), which world.Levels.RangeInfo3's TINYINT column type alone does not enforce
-- (TINYINT permits 0-255, wider than the legacy rule). Not an in-place edit of Tables/world/Levels.sql --
-- that script is already applied/journaled, so this corrective ALTER TABLE ships as its own migration
-- instead (see Database/_manifest.txt's own header and the sql-server-2025-stored-procedure-conventions
-- skill: "never edit an already-applied script").
--
-- Complements, not replaces, Application/Fenrir.Application.Game.GameData/WorldDataCacheBuilder.cs's
-- ValidateLevels boot-time check (same contract, same bound): that C# check only runs once per GameServer
-- boot against whatever is in the table at that moment, so it cannot by itself stop an out-of-range value
-- from ever being written by a future bad reseed/hand-edit/migration -- this constraint closes that gap at
-- write time, independent of any application-layer check ever running again. The other four gaps in the
-- same contract (ExpRangeMin/ExpRangeMax's 2,000,000,000 upper bound, the strict ExpRangeMin < ExpRangeMax
-- requirement, the inter-row exact-tiling requirement, and the Life/Mana 0-10000 bound) are either cross-row
-- (tiling, not expressible as a single-row CHECK) or already narrower than their own column type without
-- needing one for the currently-seeded data, so they stay C#-only validation per that method's own remarks;
-- RangeInfo3 is the one bound the contract explicitly flagged for a storage-level decision.
--
-- Safe against the current 145-row seed (Migrations/Seed/world/060_levels.sql): its RangeInfo3 values top
-- out at 50, well inside 0-100.
ALTER TABLE world.Levels
    ADD CONSTRAINT CK_Levels_RangeInfo3 CHECK (RangeInfo3 BETWEEN 0 AND 100);
GO
