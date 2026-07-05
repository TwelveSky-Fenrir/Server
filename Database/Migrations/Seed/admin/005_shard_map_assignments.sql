-- M1's single-shard default (shard 1 hosts map 1 only) -- matches the compiled-in fallback that used
-- to live in Servers/Fenrir.GameServer/appsettings.json's Game:Maps before that list moved here.
INSERT INTO admin.ShardMapAssignments (ShardId, MapId)
VALUES (1, 1);
