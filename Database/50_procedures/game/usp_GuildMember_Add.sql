-- Member-count check runs WITH (UPDLOCK, HOLDLOCK) so concurrent joins into a nearly-full guild
-- serialize instead of both passing -- no table constraint backstops this 50-member cap.
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
