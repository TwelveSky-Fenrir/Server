-- M1's single-shard default (shard 1 hosts map 1 only) -- matches the compiled-in fallback that used
-- to live in Servers/Fenrir.GameServer/appsettings.json's Game:Maps before that list moved here.
--
-- Idempotency fix (seed-audit pass, 2026-07-12): the table-level guard below matches every other bulk
-- reference-data seed under Migrations/Seed/world/** (e.g. 001_levels.sql) -- without it, a replay against
-- a database whose admin.SchemaVersions journal was lost/reset would hit PK_ShardMapAssignments/
-- UQ_ShardMapAssignments_MapId and halt the whole DbMigrator run partway through.
IF NOT EXISTS (SELECT 1
               FROM admin.ShardMapAssignments)
    BEGIN
        INSERT INTO admin.ShardMapAssignments (ShardId, MapId)
        VALUES (1, 1),
               (1, 6),
               (1, 11),
               (1, 140);
    END;
