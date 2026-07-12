-- Default admin.ServerQuota row. 1000 is a Fenrir-chosen dev/default cap (matching
-- Fenrir.Application.Login.Domain.LoginServerOptions.MaxPlayerNum's own default for the unrelated connect-time
-- greeting value), not legacy's shipped 1900 (Server/BuildEU33/DB/nxtserver.sql:320) -- an operator should
-- retune this row directly for a real deployment, not this seed script.
--
-- Idempotency fix (seed-audit pass, 2026-07-12): singleton-row guard, matching
-- Migrations/Seed/admin/012_tribe_four_quota.sql's own convention for the sibling singleton tables --
-- without it, a replay against a database whose admin.SchemaVersions journal was lost/reset would hit
-- PK_ServerQuota and halt the whole DbMigrator run partway through.
IF NOT EXISTS (SELECT 1
               FROM admin.ServerQuota
               WHERE Id = 1)
    INSERT INTO admin.ServerQuota (Id, MaxPlayers)
    VALUES (1, 1000);
