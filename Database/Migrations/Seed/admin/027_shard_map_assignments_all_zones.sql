-- Charge TOUTES les maps : assigne chaque world.Zones à shard 1 pour qu'un seul GameServer héberge la
-- totalité des zones. Étend le défaut mono-map du seed 005 (1/6/11/140) à l'ensemble du catalogue.
--
-- Additif + idempotent + count-agnostic : n'insère que les ZoneNumber PAS encore assignés (le filtre
-- corrélé NOT EXISTS exclut les maps déjà présentes, dont celles de 005), donc UQ_ShardMapAssignments_MapId
-- et la FK vers world.Zones sont toujours satisfaites, et un rejeu est un no-op. S'exécute après le seed
-- world.Zones (007_zones, plus haut dans le manifeste) : la FK est garantie peuplée.
INSERT INTO admin.ShardMapAssignments (ShardId, MapId)
SELECT 1, z.ZoneNumber
FROM world.Zones AS z
WHERE NOT EXISTS (SELECT 1
                  FROM admin.ShardMapAssignments AS a
                  WHERE a.MapId = z.ZoneNumber);
