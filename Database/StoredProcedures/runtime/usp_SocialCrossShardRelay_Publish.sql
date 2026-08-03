CREATE PROCEDURE runtime.usp_SocialCrossShardRelay_Publish @Kind TINYINT,
                                                           @MessageType TINYINT,
                                                           @Accepted BIT NULL,
                                                           @ReasonCode TINYINT NULL,
                                                           @SourceShardId TINYINT,
                                                           @SourceCharacterId INT,
                                                           @SourceAvatarName NVARCHAR(13),
                                                           @TargetShardId TINYINT,
                                                           @TargetCharacterId INT,
                                                           @AskRelayId BIGINT NULL,
                                                           @CorrelationId UNIQUEIDENTIFIER
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE @ExistingRelayId BIGINT = NULL;

    SELECT @ExistingRelayId = RelayId
    FROM runtime.SocialCrossShardRelay
    WHERE CorrelationId = @CorrelationId;

    IF @ExistingRelayId IS NULL
        INSERT INTO runtime.SocialCrossShardRelay
        (Kind, MessageType, Accepted, ReasonCode, SourceShardId, SourceCharacterId, SourceAvatarName,
         TargetShardId, TargetCharacterId, AskRelayId, CorrelationId, CreatedAtUtc)
        VALUES (@Kind, @MessageType, @Accepted, @ReasonCode, @SourceShardId, @SourceCharacterId,
                @SourceAvatarName, @TargetShardId, @TargetCharacterId, @AskRelayId, @CorrelationId,
                SYSUTCDATETIME());
END;
