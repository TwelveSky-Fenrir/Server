CREATE PROCEDURE game.usp_Zone195NokSanState_Get
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    SELECT Revision, OwnerSlot0, OwnerSlot2, OwnerSlot3, StonesHeld0, StonesHeld1, StonesHeld2, StonesHeld3,
           UpdatedAtUtc
    FROM game.Zone195NokSanStates WITH (HOLDLOCK)
    WHERE StateId = 1;

    SELECT MapId, Phase, CapturerCharacterId, CapturerTribe, CapturerName, RemainingTime, PhaseAccumulatorTicks
    FROM game.Zone195NokSanCaptures
    WHERE StateId = 1
    ORDER BY MapId;

    COMMIT TRANSACTION;
END;
