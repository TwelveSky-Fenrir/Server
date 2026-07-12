-- Default admin-tunable settings row (see admin.GameSetting's own header for why this table exists
-- at all -- only Fenrir-invented values with no legacy source to stay iso to belong here).
--
-- Idempotency fix (seed-audit pass, 2026-07-12): singleton-row guard, matching
-- Migrations/Seed/admin/012_tribe_four_quota.sql's own convention for the sibling singleton tables --
-- without it, a replay against a database whose admin.SchemaVersions journal was lost/reset would hit
-- PK_GameSetting and halt the whole DbMigrator run partway through.
IF NOT EXISTS (SELECT 1
               FROM admin.GameSetting
               WHERE Id = 1)
    INSERT INTO admin.GameSetting (Id, ProxyShopDurationDays)
    VALUES (1, 7);
