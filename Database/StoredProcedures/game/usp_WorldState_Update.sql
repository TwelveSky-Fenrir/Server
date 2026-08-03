CREATE PROCEDURE game.usp_WorldState_Update @Zone038WinTribe TINYINT = NULL,
                                            @Zone038WinTribeTime INT = NULL,
                                            @TribeSymbolBattle BIT,
                                            @MonsterSymbol TINYINT = NULL,
                                            @MonsterSymbolEndTime INT = NULL,
                                            @HighTribe TINYINT = NULL,
                                            @UpdateTribePoint SMALLINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.WorldState
    SET Zone038WinTribe      = @Zone038WinTribe,
        Zone038WinTribeTime  = @Zone038WinTribeTime,
        TribeSymbolBattle    = @TribeSymbolBattle,
        MonsterSymbol        = @MonsterSymbol,
        MonsterSymbolEndTime = @MonsterSymbolEndTime,
        HighTribe            = @HighTribe,
        UpdateTribePoint     = @UpdateTribePoint,
        UpdatedAtUtc         = SYSUTCDATETIME()
    WHERE Id = 1;
END;
