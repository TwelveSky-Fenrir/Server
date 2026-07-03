-- Member count per guild. Started as an INDEXED (materialized) view during design -- verified it
-- compiles and works end-to-end against a real SQL Server 2025 instance -- but deliberately reverted to
-- a plain view after that same testing surfaced a real cost: an indexed view on game.GuildMembers forces
-- ANSI_NULLS/ANSI_PADDING/ANSI_WARNINGS/ARITHABORT/CONCAT_NULL_YIELDS_NULL/QUOTED_IDENTIFIER to the
-- "indexed view" required values for EVERY future INSERT/UPDATE/DELETE against that table, forever, in
-- every session that touches it (Microsoft SQL Server indexed-view SET-option requirements) --
-- ADO.NET/CaeriusNet connections satisfy this today (ODBC/OLE DB clients set ANSI_WARNINGS ON, which
-- implicitly sets ARITHABORT ON at this DB's compatibility level), but any other tool (sqlcmd, a future
-- admin/import script) that writes to GuildMembers without the right SET options would hit error 1934 --
-- confirmed by hand against a live container. game.GuildMembers is real, frequently-mutated player state
-- (players join/leave guilds constantly), not read-mostly reference data, so it is exactly the wrong
-- table to add that permanent footgun to for a headcount that is cheap to compute live anyway (capped
-- at MAX_GUILD_AVATAR_NUM=50 members per guild). The task brief explicitly allows this fallback
-- ("otherwise a plain view is fine too").
-- Consumed internally by game.usp_Guild_GetAll -- never granted/queried directly by app code
-- (Database/60_permissions/001_roles.sql).
CREATE VIEW game.vw_GuildRosterCounts
AS
SELECT GuildId,
       COUNT(*) AS MemberCount
FROM game.GuildMembers
GROUP BY GuildId;
