CREATE PROCEDURE game.usp_Guild_SetBuffTime @GuildId INT,
                                            @BuffTime INT,
                                            @BuffTimeForDiff BIGINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.Guilds
    SET BuffTime        = @BuffTime,
        BuffTimeForDiff = @BuffTimeForDiff,
        UpdatedAtUtc    = SYSUTCDATETIME()
    WHERE GuildId = @GuildId;

    IF
        @@ROWCOUNT = 0
        THROW 50235, N'Guild not found.', 1;
END;
