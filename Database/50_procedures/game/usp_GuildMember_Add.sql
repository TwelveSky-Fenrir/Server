-- database/50_procedures/game/usp_GuildMember_Add.sql
-- Contract: enroll one character into a guild (replaces ts25extra's guild-join tSort -- report 06 §3.1).
-- Params:
--   @GuildId     INT
--   @CharacterId INT
--   @Role        TINYINT = 0 -- game.GuildMembers role enum (0 member, 1 sub-master, 2 master);
--                              CK_GuildMembers_Role is the validity backstop
-- Result set: none.
-- Idempotent: no (a repeat call throws 50231 -- the character is then already in a guild).
-- The member-count gate (MAX_GUILD_AVATAR_NUM = 50, the legacy gMember blob's fixed capacity) is read
-- WITH (UPDLOCK, HOLDLOCK) inside the transaction so two concurrent joins into a 49-member guild
-- serialize instead of both passing the count check -- the one place a pre-check cannot be left as a
-- racy courtesy, because no table constraint backstops a per-guild cardinality.
-- Errors:
--   THROW 50235 -- guild not found (admin.ErrorCatalog, 502xx = game range).
--   THROW 50231 -- character already belongs to a guild.
--   THROW 50232 -- guild is full (50 members).
CREATE PROCEDURE game.usp_GuildMember_Add @GuildId     INT,
    @CharacterId INT,
    @Role        TINYINT = 0
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    IF
NOT EXISTS (SELECT 1 FROM game.Guilds WHERE GuildId = @GuildId)
        THROW 50235, N'Guild not found.', 1;

    IF
EXISTS (SELECT 1 FROM game.GuildMembers WHERE CharacterId = @CharacterId)
        THROW 50231, N'Character already belongs to a guild.', 1;

BEGIN
TRANSACTION;

    IF
(
SELECT COUNT(*)
FROM game.GuildMembers WITH (UPDLOCK, HOLDLOCK)
WHERE GuildId = @GuildId) >= 50
    THROW 50232
    , N'Guild is full (50 members).'
    , 1;

INSERT INTO game.GuildMembers (GuildId, CharacterId, Role)
VALUES (@GuildId, @CharacterId, @Role);

COMMIT TRANSACTION;
END;
