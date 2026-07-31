-- Legacy MyDB::UpdateGuildBuffTime writes gBuffTime/gBuffTimeForDiff ONLY and never touches
-- gBuffType/gBuffState (Server/ts25extra/S08_MyDB.cpp:1151-1186). usp_Guild_SetBuff writes all four
-- columns, so a decay/top-up caller using it silently reverts a concurrent buff-type change made between
-- its own read and its write. BuffTimeForDiff is a legacy time_t: EPOCH SECONDS, not .NET ticks
-- (Server/ts25extra/S08_MyDB.cpp:1169-1174).
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
