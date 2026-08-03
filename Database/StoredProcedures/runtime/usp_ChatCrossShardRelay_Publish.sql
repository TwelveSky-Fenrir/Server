CREATE PROCEDURE runtime.usp_ChatCrossShardRelay_Publish @SourceShardId TINYINT,
                                                         @SourceCharacterId INT,
                                                         @SourceAvatarName NVARCHAR(13),
                                                         @TargetShardId TINYINT,
                                                         @TargetCharacterId INT,
                                                         @TargetAvatarName NVARCHAR(13),
                                                         @Content NVARCHAR(61),
                                                         @SenderAuthType TINYINT,
                                                         @CorrelationId UNIQUEIDENTIFIER
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE @ExistingRelayId BIGINT = NULL;

    SELECT @ExistingRelayId = RelayId
    FROM runtime.ChatCrossShardRelay
    WHERE CorrelationId = @CorrelationId;

    IF @ExistingRelayId IS NULL
        INSERT INTO runtime.ChatCrossShardRelay
        (SourceShardId, SourceCharacterId, SourceAvatarName, TargetShardId, TargetCharacterId, TargetAvatarName,
         Content, SenderAuthType, CorrelationId, CreatedAtUtc)
        VALUES (@SourceShardId, @SourceCharacterId, @SourceAvatarName, @TargetShardId, @TargetCharacterId,
                @TargetAvatarName, @Content, @SenderAuthType, @CorrelationId, SYSUTCDATETIME());
END;
