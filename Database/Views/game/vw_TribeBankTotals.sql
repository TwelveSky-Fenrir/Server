-- Cross-shard-visible running total per tribe. game.TribeBank is MEMORY_OPTIMIZED and the hottest write
-- path in this domain (usp_TribeBank_Deposit/usp_TribeBank_Withdraw fire on every player deposit/
-- withdrawal, usp_TribeBank_ApplyTaxSweep sweeps it roughly every 10 zone-uptime minutes), reachable from
-- whichever shard hosts the acting player/zone -- so a tribe's total bank balance is a genuinely
-- cluster-wide shared number, not something any one GameServer process could cache in memory the way it
-- already does for boot-time reference/world-state data: a per-shard cache would desync the instant a
-- DIFFERENT shard's deposit/withdraw/tax-sweep fires. This view is the single-round-trip aggregation point
-- instead of every future caller duplicating the SUM/GROUP BY itself.
--
-- Deliberately a PLAIN view, and this one is not merely a bad trade-off but flatly IMPOSSIBLE as an indexed
-- one: Microsoft Learn's "Accessing Memory-Optimized Tables Using Interpreted Transact-SQL" lists
-- "Referencing a memory-optimized table from an indexed view" as an explicitly unsupported construct --
-- CREATE UNIQUE CLUSTERED INDEX on this view would fail outright, independent of the write-frequency
-- argument that already disqualifies game.vw_GuildRanking/game.vw_GuildRosterCounts.
--
-- TotalAmount is summed as BIGINT, not INT: game.TribeBank.Amount is capped per-slot at 2,000,000,000
-- (MAX_NUMBER_SIZE, usp_TribeBank_ApplyTaxSweep's own @Ceiling) but there are 50 slots per tribe, so the
-- theoretical total can exceed INT range -- SQL Server's SUM() over an INT column returns INT by default
-- and would silently be at overflow risk here if left uncast.
-- A tribe with zero rows in game.TribeBank (never deposited into) produces no row here -- callers wanting
-- all 4 tribes represented LEFT JOIN game.Tribes onto this view and COALESCE both aggregates to 0, the same
-- convention game.usp_Guild_GetAll already applies over game.vw_GuildRosterCounts.
--
-- WITH (SNAPSHOT) on game.TribeBank is required here, not optional, and this was caught by an actual test
-- failure (SQL error 41359/MSSQLSERVER_41359), not reasoned out in advance: Fenrir runs with
-- READ_COMMITTED_SNAPSHOT ON database-wide, and under RCSI a single statement can't read a memory-optimized
-- table under READ COMMITTED while ALSO touching a disk-based table -- even in autocommit mode (the
-- autocommit exemption only covers 41368/explicit-transaction cases, not 41359/RCSI-mixing). The exact
-- failing shape was usp_TribeBank_GetTotals's own LEFT JOIN of this view onto the disk-based game.Tribes;
-- putting the hint on the base-table reference here (rather than on every future consumer) fixes every
-- caller at the source, matching how usp_TribeBank_Withdraw/usp_TribeBank_ApplyTaxSweep already hint their
-- own direct game.TribeBank reads.
CREATE VIEW game.vw_TribeBankTotals
AS
SELECT TribeId,
       SUM(CAST(Amount AS BIGINT)) AS TotalAmount,
       COUNT(*)                    AS OccupiedSlotCount
FROM game.TribeBank WITH (SNAPSHOT)
GROUP BY TribeId;
