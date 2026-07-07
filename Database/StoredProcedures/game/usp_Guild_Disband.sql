-- game.GuildNotices access requires WITH (SNAPSHOT): the database does not set
-- MEMORY_OPTIMIZED_ELEVATE_TO_SNAPSHOT, so the hint is mandatory here, not defensive.
CREATE PROCEDURE game.usp_Guild_Disband @GuildId INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN
        TRANSACTION;

    DELETE
    FROM game.GuildNotices WITH (SNAPSHOT)
    WHERE GuildId = @GuildId;

    DELETE
    FROM game.GuildMembers
    WHERE GuildId = @GuildId;

    DELETE
    FROM game.Guilds
    WHERE GuildId = @GuildId;

    IF
        @@ROWCOUNT = 0
        THROW 50235, N'Guild not found.', 1;

    COMMIT TRANSACTION;
END;
