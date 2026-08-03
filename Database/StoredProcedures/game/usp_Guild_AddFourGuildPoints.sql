CREATE PROCEDURE game.usp_Guild_AddFourGuildPoints @GuildId INT,
                                                   @Delta INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.Guilds
    SET Points       = Points + @Delta,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE GuildId = @GuildId
      AND Points + @Delta >= 0;
END;
