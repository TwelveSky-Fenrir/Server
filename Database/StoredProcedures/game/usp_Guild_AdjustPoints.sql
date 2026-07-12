-- A missing GuildId is indistinguishable from insufficient points here (both = 0 rows updated);
-- the error text covers both cases.
CREATE PROCEDURE game.usp_Guild_AdjustPoints @GuildId INT,
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

    IF
        @@ROWCOUNT = 0
        THROW 50234, N'Unknown guild or insufficient guild points for this adjustment.', 1;
END;
