CREATE OR ALTER PROCEDURE runtime.usp_GuildTribeBroadcastRelay_Acknowledge @ShardId TINYINT,
                                                                           @RelayId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @RelayId <= 0
        THROW 51083, 'Guild/tribe relay acknowledgement requires a positive relay identifier.', 1;

    DECLARE @CurrentRelayId BIGINT = NULL;
    DECLARE @SourceShardId TINYINT = NULL;

    BEGIN TRANSACTION;

    SELECT @SourceShardId = SourceShardId
    FROM runtime.GuildTribeBroadcastRelay WITH (SNAPSHOT)
    WHERE RelayId = @RelayId;

    IF @SourceShardId IS NULL
        THROW 51084, 'Guild/tribe relay acknowledgement references an unknown relay.', 1;

    IF @SourceShardId = @ShardId
        THROW 51085, 'Guild/tribe relay acknowledgement cannot acknowledge a source-shard relay.', 1;

    SELECT @CurrentRelayId = LastRelayId
    FROM runtime.GuildTribeBroadcastCursor WITH (SNAPSHOT)
    WHERE ShardId = @ShardId;

    IF EXISTS
    (
        SELECT 1
        FROM runtime.GuildTribeBroadcastRelay WITH (SNAPSHOT)
        WHERE RelayId > COALESCE(@CurrentRelayId, 0)
          AND RelayId < @RelayId
          AND SourceShardId <> @ShardId
    )
        THROW 51086, 'Guild/tribe relay acknowledgement cannot skip an earlier delivery.', 1;

    IF @CurrentRelayId IS NULL
        INSERT INTO runtime.GuildTribeBroadcastCursor (ShardId, LastRelayId)
        VALUES (@ShardId, @RelayId);
    ELSE IF @CurrentRelayId < @RelayId
        UPDATE runtime.GuildTribeBroadcastCursor
        SET LastRelayId = @RelayId
        WHERE ShardId = @ShardId;

    COMMIT TRANSACTION;
END;
