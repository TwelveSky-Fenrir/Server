-- database/50_procedures/game/usp_WorldState_EnsureInitialized_FixMissingTribeSeed.sql
-- Corrective follow-up to usp_WorldState_EnsureInitialized.sql -- that script is never edited in place once
-- applied (Fenrir.Tools.DbMigrator journals every script by content hash and refuses a changed re-apply; see
-- its own top comment). The original body inserted game.WorldStateTribes rows 0-3 without first guaranteeing
-- game.Tribes 0-3 exist. game.Tribes has no seed script of its own (Database/70_seed only seeds admin/world),
-- so on a genuinely fresh database this proc's very first call fails outright on
-- FK_WorldStateTribes_Tribe. This redefinition seeds the 4 fixed game.Tribes rows (MAX_TRIBE_NUM=4, domain
-- 0-3, see game.Tribes.sql) before anything that references them.
CREATE
OR
ALTER PROCEDURE game.usp_WorldState_EnsureInitialized
    AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    IF
NOT EXISTS (SELECT 1 FROM game.Tribes WHERE TribeId = 0)
        INSERT INTO game.Tribes (TribeId) VALUES (0), (1), (2), (3);

    IF
NOT EXISTS (SELECT 1 FROM game.WorldState WHERE Id = 1)
        INSERT INTO game.WorldState (Id) VALUES (1);

    IF
NOT EXISTS (SELECT 1 FROM game.WorldStateTribes)
BEGIN
INSERT INTO game.WorldStateTribes (TribeId, SymbolDate, HasSymbol, Points, IsClosed)
VALUES (0, NULL, 1, 0, 0),
       (1, NULL, 1, 0, 0),
       (2, NULL, 1, 0, 0),
       (3, NULL, 1, 0, 0);
END;
END;
