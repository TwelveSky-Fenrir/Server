-- game.Characters.RebirthCount -- defense-in-depth bound matching MAX_REBIRTH_LIMIT (12, G1-G12), the
-- absolute cap both rebirth-advancement trigger paths already enforce in the application layer before
-- ever incrementing this column (RebirthProgression.MaxRebirthGeneration,
-- Application/Fenrir.Application.Game.Domain/Progression/RebirthProgression.cs) -- Path A (Rebirth Pill,
-- UseInventoryItemService.ResolveRebirthPillAsync) and Path B (Max Rebirth,
-- TribeActionService.RebirthAsync, additionally app-capped at generation 6 of its own).
--
-- This column's only two writers are usp_Character_CreateWithStarterKit (DEFAULT 0) and
-- usp_Character_PersistProgressBatch's write-behind flush of PlayerRuntimeState.RebirthCount, neither of
-- which re-validates the bound at the SQL layer today -- same "push the invariant into the database, not
-- just C#" posture as CK_Characters_Exp2 (Tables/game/Characters.sql) and CK_Characters_Zone241Time
-- (Migrations/041_character_rebirth_zone241_time.sql). Closes the gap so a future application-layer bug
-- (a new caller mutating RebirthCount some other way, or a corrupted/replayed write-behind batch) cannot
-- silently persist an out-of-range value that downstream consumers (StatCalculator's critical/
-- critical-defence bonus tables, TribePointLevelRecompute, TribeVoteElection, EquipItemValidationGate's
-- rebirth gates) would then read as truth.
--
-- Added as a standalone migration (not folded into Tables/game/Characters.sql) since that script is
-- already applied: Fenrir.Tools.DbMigrator journals every script by content hash and refuses a changed
-- re-apply. Not edited in place once applied, same posture as every other migration in this list -- a
-- later correction is a new migration, never an edit of this one.
ALTER TABLE game.Characters
    ADD CONSTRAINT CK_Characters_RebirthCount CHECK (RebirthCount >= 0 AND RebirthCount <= 12);
GO
