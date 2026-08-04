CREATE PROCEDURE runtime.usp_RvrSiegeEventRelay_Acknowledge @ShardId TINYINT,
                                                            @RelayId BIGINT
    WITH NATIVE_COMPILATION, SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE @CurrentRelayId BIGINT = NULL;

    SELECT @CurrentRelayId = LastRelayId
    FROM runtime.RvrSiegeEventRelayCursor
    WHERE ShardId = @ShardId;

    IF @CurrentRelayId IS NULL
    BEGIN
        INSERT INTO runtime.RvrSiegeEventRelayCursor (ShardId, LastRelayId)
        VALUES (@ShardId, @RelayId);
    END
    ELSE
    BEGIN
        IF @CurrentRelayId < @RelayId
            UPDATE runtime.RvrSiegeEventRelayCursor
            SET LastRelayId = @RelayId
            WHERE ShardId = @ShardId;
    END
END;
