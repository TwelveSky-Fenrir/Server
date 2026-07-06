-- Default admin.ServerQuota row. 1000 is a Fenrir-chosen dev/default cap (matching
-- Fenrir.Application.Login.Domain.LoginServerOptions.MaxPlayerNum's own default for the unrelated connect-time
-- greeting value), not legacy's shipped 1900 (Server/BuildEU33/DB/nxtserver.sql:320) -- an operator should
-- retune this row directly for a real deployment, not this seed script.
INSERT INTO admin.ServerQuota (Id, MaxPlayers)
VALUES (1, 1000);
