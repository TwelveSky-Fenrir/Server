-- Legacy: gBuffType/gBuffState/gBuffTime/gBuffTimeForDiff. Activation is this update plus an
-- in-process notification only; the legacy center rebroadcast is not implemented.
CREATE PROCEDURE game.usp_Guild_SetBuff @GuildId INT,
                                        @BuffType INT,
                                        @BuffState INT,
                                        @BuffTime INT,
                                        @BuffTimeForDiff BIGINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.Guilds
    SET BuffType        = @BuffType,
        BuffState       = @BuffState,
        BuffTime        = @BuffTime,
        BuffTimeForDiff = @BuffTimeForDiff
    WHERE GuildId = @GuildId;

    IF
        @@ROWCOUNT = 0
        THROW 50235, N'Guild not found.', 1;
END;
