CREATE PROCEDURE game.usp_WorldState_Update @Zone038WinTribe TINYINT = NULL,
                                            @Zone038WinTribeTime INT = NULL,
                                            @TribeSymbolBattle BIT,
                                            @MonsterSymbol TINYINT = NULL,
                                            @MonsterSymbolEndTime INT = NULL,
                                            @HighTribe TINYINT = NULL,
                                            @UpdateTribePoint SMALLINT,
                                            @ExpectedRevision BIGINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF @ExpectedRevision IS NULL OR @ExpectedRevision < 0
        THROW 51207, N'A world-state revision must be nonnegative.', 1;

    UPDATE game.WorldState
    SET Zone038WinTribe      = @Zone038WinTribe,
        Zone038WinTribeTime  = @Zone038WinTribeTime,
        TribeSymbolBattle    = @TribeSymbolBattle,
        MonsterSymbol        = @MonsterSymbol,
        MonsterSymbolEndTime = @MonsterSymbolEndTime,
        HighTribe            = @HighTribe,
        UpdateTribePoint     = @UpdateTribePoint,
        Revision             = Revision + 1,
        UpdatedAtUtc         = SYSUTCDATETIME()
    WHERE Id = 1
      AND Revision = @ExpectedRevision;

    SELECT Applied = CAST(CASE WHEN @@ROWCOUNT = 1 THEN 1 ELSE 0 END AS BIT);
END;
