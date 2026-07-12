CREATE PROCEDURE game.usp_Guild_SetGrade @GuildId INT,
                                         @Grade INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.Guilds
    SET Grade        = @Grade,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE GuildId = @GuildId;

    IF
        @@ROWCOUNT = 0
        THROW 50235, N'Guild not found.', 1;
END;
