-- Legacy MyDB::UpdateGuildBuffType writes gBuffType/gBuffState ONLY and never touches
-- gBuffTime/gBuffTimeForDiff (Server/ts25extra/S08_MyDB.cpp:1188-1191, which hardcodes gBuffState=1).
-- Selecting a buff type must not carry a stale BuffTime read back to the row: usp_Guild_SetBuff writes
-- all four columns, so it destroys minutes credited by a concurrent recharge or decay flush.
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
