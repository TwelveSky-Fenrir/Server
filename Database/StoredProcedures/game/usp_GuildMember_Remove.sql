CREATE PROCEDURE game.usp_GuildMember_Remove @GuildId INT,
                                             @CharacterId INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DELETE
    FROM game.GuildMembers
    WHERE GuildId = @GuildId
      AND CharacterId = @CharacterId;
END;
