CREATE OR ALTER PROCEDURE runtime.usp_PartyResyncRelay_Acknowledge @ShardId TINYINT,
                                                                   @RelayId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ShardId IS NULL
        THROW 51087, 'Party resync relay acknowledgement requires a shard identifier.', 1;

    IF @RelayId <= 0
        THROW 51088, 'Party resync relay acknowledgement requires a positive relay identifier.', 1;

    DECLARE @CurrentRelayId BIGINT = NULL;
    DECLARE @SourceShardId TINYINT = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @SourceShardId = SourceShardId
        FROM runtime.PartyResyncRelay WITH (SNAPSHOT)
        WHERE RelayId = @RelayId;

        IF @SourceShardId IS NULL
            THROW 51089, 'Party resync relay acknowledgement references an unknown relay.', 1;

        IF @SourceShardId = @ShardId
            THROW 51090, 'Party resync relay acknowledgement cannot acknowledge a source-shard relay.', 1;

        SELECT @CurrentRelayId = LastRelayId
        FROM runtime.PartyResyncRelayCursor WITH (SNAPSHOT)
        WHERE ShardId = @ShardId;

        IF EXISTS
        (
            SELECT 1
            FROM runtime.PartyResyncRelay WITH (SNAPSHOT)
            WHERE RelayId > COALESCE(@CurrentRelayId, 0)
              AND RelayId < @RelayId
              AND SourceShardId <> @ShardId
        )
            THROW 51091, 'Party resync relay acknowledgement cannot skip an earlier delivery.', 1;

        IF @CurrentRelayId IS NULL
            INSERT INTO runtime.PartyResyncRelayCursor (ShardId, LastRelayId)
            VALUES (@ShardId, @RelayId);
        ELSE IF @CurrentRelayId < @RelayId
            UPDATE runtime.PartyResyncRelayCursor
            SET LastRelayId = @RelayId
            WHERE ShardId = @ShardId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH
END;
