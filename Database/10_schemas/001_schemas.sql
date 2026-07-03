-- M1 schemas (architecture reference §12.2) plus `world` (added post-M1: legacy static-reference-data
-- migration, ADR-0003's "hors périmètre M1" deferral now being picked up) -- `social`/`telemetry` remain
-- deferred.

CREATE SCHEMA auth AUTHORIZATION dbo;
GO

CREATE SCHEMA game AUTHORIZATION dbo;
GO

CREATE SCHEMA runtime AUTHORIZATION dbo;
GO

CREATE SCHEMA admin AUTHORIZATION dbo;
GO

-- Read-mostly reference/master data migrated from the legacy 005_0000N.IMG tables, 002.BIN/003.BIN, and
-- the .WREGION.csv monster-spawn files (items, skills, monsters+drops, npcs+shop/menu, quests, levels,
-- gem sockets, zone NPC placement/portals, monster spawn regions) plus a handful of small legacy MySQL
-- config tables (item-mall catalog, blood-exchange, event definitions, reward bundles).
CREATE SCHEMA world AUTHORIZATION dbo;
GO
