CREATE PROCEDURE game.usp_Guild_SetBuffType @GuildId INT,
                                            @BuffType INT,
                                            @BuffState INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.Guilds
    SET BuffType     = @BuffType,
        BuffState    = @BuffState,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE GuildId = @GuildId;

    IF
        @@ROWCOUNT = 0
        THROW 50235, N'Guild not found.', 1;
END;
