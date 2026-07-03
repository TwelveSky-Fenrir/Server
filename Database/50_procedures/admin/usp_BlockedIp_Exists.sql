-- Contract: @IpAddress VARCHAR(45) -> RS0, 0 or 1 row (empty result set = not blocked).
-- Natively compiled against admin.BlockedIps (memory-optimized, SCHEMA_AND_DATA, see its table
-- comment): this is checked on literally every inbound connection attempt at both LoginServer and
-- GameServer, so it runs with zero locks/latches and zero T-SQL interpretation -- exactly the
-- "hottest read on the connection path" case runtime.usp_SessionTicket_Create/game.usp_TribeBank_Deposit
-- already justify native compilation for.
-- No "IF EXISTS(...)" here: natively compiled modules do not support EXISTS inside IF/WHILE
-- conditionals. The plain SELECT-with-WHERE-clause below is both supported (EXISTS/predicates in a
-- SELECT's WHERE clause are fine) and matches the existing "empty result set means no match" convention
-- (auth.usp_Account_Authenticate, world.usp_Monster_GetById) instead of a CASE-derived BIT flag.
CREATE PROCEDURE admin.usp_BlockedIp_Exists @IpAddress VARCHAR(45)
WITH NATIVE_COMPILATION, SCHEMABINDING
AS
BEGIN ATOMIC
WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
SELECT BlockedIpId, CreatedAtUtc
FROM admin.BlockedIps
WHERE IpAddress = @IpAddress;
END;
