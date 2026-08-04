CREATE PROCEDURE game.usp_Zone195NokSanState_TrySave @ExpectedRevision BIGINT,
                                                     @OwnerSlot0 TINYINT,
                                                     @OwnerSlot2 TINYINT,
                                                     @OwnerSlot3 TINYINT,
                                                     @StonesHeld0 TINYINT,
                                                     @StonesHeld1 TINYINT,
                                                     @StonesHeld2 TINYINT,
                                                     @StonesHeld3 TINYINT,
                                                     @Capture99Phase TINYINT,
                                                     @Capture99CharacterId INT,
                                                     @Capture99Tribe TINYINT,
                                                     @Capture99Name NVARCHAR(13),
                                                     @Capture99RemainingTime INT,
                                                     @Capture99PhaseAccumulatorTicks INT,
                                                     @Capture100Phase TINYINT,
                                                     @Capture100CharacterId INT,
                                                     @Capture100Tribe TINYINT,
                                                     @Capture100Name NVARCHAR(13),
                                                     @Capture100RemainingTime INT,
                                                     @Capture100PhaseAccumulatorTicks INT,
                                                     @Capture196Phase TINYINT,
                                                     @Capture196CharacterId INT,
                                                     @Capture196Tribe TINYINT,
                                                     @Capture196Name NVARCHAR(13),
                                                     @Capture196RemainingTime INT,
                                                     @Capture196PhaseAccumulatorTicks INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ExpectedRevision < 0
        THROW 50761, N'A Nok-San expected revision cannot be negative.', 1;

    IF @OwnerSlot0 > 4 OR @OwnerSlot2 > 4 OR @OwnerSlot3 > 4 OR
       @StonesHeld0 > 4 OR @StonesHeld1 > 4 OR @StonesHeld2 > 4 OR @StonesHeld3 > 4
        THROW 50762, N'Nok-San owner/count values must be in 0..4.', 1;

    DECLARE @ExpectedStonesHeld0 TINYINT =
        IIF(@OwnerSlot0 = 1, 1, 0) + IIF(@OwnerSlot2 = 1, 1, 0) + IIF(@OwnerSlot3 = 1, 1, 0);
    DECLARE @ExpectedStonesHeld1 TINYINT =
        IIF(@OwnerSlot0 = 2, 1, 0) + IIF(@OwnerSlot2 = 2, 1, 0) + IIF(@OwnerSlot3 = 2, 1, 0);
    DECLARE @ExpectedStonesHeld2 TINYINT =
        IIF(@OwnerSlot0 = 3, 1, 0) + IIF(@OwnerSlot2 = 3, 1, 0) + IIF(@OwnerSlot3 = 3, 1, 0);
    DECLARE @ExpectedStonesHeld3 TINYINT =
        IIF(@OwnerSlot0 = 4, 1, 0) + IIF(@OwnerSlot2 = 4, 1, 0) + IIF(@OwnerSlot3 = 4, 1, 0);

    IF @StonesHeld0 <> @ExpectedStonesHeld0 OR @StonesHeld1 <> @ExpectedStonesHeld1 OR
       @StonesHeld2 <> @ExpectedStonesHeld2 OR @StonesHeld3 <> @ExpectedStonesHeld3
        THROW 50763, N'Nok-San stone counts must exactly match the active-site owners.', 1;

    DECLARE @Captures TABLE
                      (
                          MapId                 SMALLINT     NOT NULL PRIMARY KEY,
                          Phase                 TINYINT      NOT NULL,
                          CapturerCharacterId   INT          NOT NULL,
                          CapturerTribe         TINYINT      NOT NULL,
                          CapturerName          NVARCHAR(13) NOT NULL,
                          RemainingTime         INT          NOT NULL,
                          PhaseAccumulatorTicks INT          NOT NULL
                      );

    INSERT INTO @Captures
    (MapId, Phase, CapturerCharacterId, CapturerTribe, CapturerName, RemainingTime, PhaseAccumulatorTicks)
    VALUES (99, @Capture99Phase, @Capture99CharacterId, @Capture99Tribe, @Capture99Name, @Capture99RemainingTime,
            @Capture99PhaseAccumulatorTicks),
           (100, @Capture100Phase, @Capture100CharacterId, @Capture100Tribe, @Capture100Name,
            @Capture100RemainingTime, @Capture100PhaseAccumulatorTicks),
           (196, @Capture196Phase, @Capture196CharacterId, @Capture196Tribe, @Capture196Name,
            @Capture196RemainingTime, @Capture196PhaseAccumulatorTicks);

    IF EXISTS
        (SELECT 1
         FROM @Captures
         WHERE Phase NOT BETWEEN 0 AND 2
            OR CapturerCharacterId < -1
            OR CapturerTribe > 3
            OR RemainingTime < 0
            OR PhaseAccumulatorTicks < 0
            OR (Phase = 0 AND (CapturerCharacterId <> -1 OR CapturerTribe <> 0 OR CapturerName <> N'' OR
                               RemainingTime <> 0 OR PhaseAccumulatorTicks <> 0))
            OR (Phase IN (1, 2) AND (CapturerCharacterId <= 0 OR CapturerName = N'')))
        THROW 50764, N'The Nok-San capture snapshot is structurally invalid.', 1;

    DECLARE @CurrentRevision BIGINT;
    DECLARE @Applied BIT = 0;

    BEGIN TRANSACTION;

    SELECT @CurrentRevision = Revision
    FROM game.Zone195NokSanStates
    WITH (UPDLOCK, HOLDLOCK)
    WHERE StateId = 1;

    IF @CurrentRevision IS NULL
        BEGIN
            IF @ExpectedRevision = 0
                BEGIN
                    INSERT INTO game.Zone195NokSanStates
                    (StateId, Revision, OwnerSlot0, OwnerSlot2, OwnerSlot3, StonesHeld0, StonesHeld1, StonesHeld2,
                     StonesHeld3)
                    VALUES (1, 1, @OwnerSlot0, @OwnerSlot2, @OwnerSlot3, @StonesHeld0, @StonesHeld1, @StonesHeld2,
                            @StonesHeld3);

                    INSERT INTO game.Zone195NokSanCaptures
                    (MapId, StateId, Phase, CapturerCharacterId, CapturerTribe, CapturerName, RemainingTime,
                     PhaseAccumulatorTicks)
                    SELECT MapId,
                           1,
                           Phase,
                           CapturerCharacterId,
                           CapturerTribe,
                           CapturerName,
                           RemainingTime,
                           PhaseAccumulatorTicks
                    FROM @Captures;

                    SET @Applied = 1;
                END;
        END
    ELSE
        IF @CurrentRevision = @ExpectedRevision
            BEGIN
                UPDATE game.Zone195NokSanStates
                SET Revision     = Revision + 1,
                    OwnerSlot0   = @OwnerSlot0,
                    OwnerSlot2   = @OwnerSlot2,
                    OwnerSlot3   = @OwnerSlot3,
                    StonesHeld0  = @StonesHeld0,
                    StonesHeld1  = @StonesHeld1,
                    StonesHeld2  = @StonesHeld2,
                    StonesHeld3  = @StonesHeld3,
                    UpdatedAtUtc = SYSUTCDATETIME()
                WHERE StateId = 1
                  AND Revision = @ExpectedRevision;

                IF @@ROWCOUNT = 1
                    BEGIN
                        UPDATE persisted
                        SET Phase                 = incoming.Phase,
                            CapturerCharacterId   = incoming.CapturerCharacterId,
                            CapturerTribe         = incoming.CapturerTribe,
                            CapturerName          = incoming.CapturerName,
                            RemainingTime         = incoming.RemainingTime,
                            PhaseAccumulatorTicks = incoming.PhaseAccumulatorTicks,
                            UpdatedAtUtc          = SYSUTCDATETIME()
                        FROM game.Zone195NokSanCaptures AS persisted
                                 JOIN @Captures AS incoming ON incoming.MapId = persisted.MapId
                        WHERE persisted.StateId = 1;

                        IF @@ROWCOUNT <> 3
                            THROW 50765, N'The Nok-San capture rows are incomplete for the singleton state.', 1;

                        SET @Applied = 1;
                    END;
            END;

    COMMIT TRANSACTION;

    SELECT Applied = @Applied;
END;
