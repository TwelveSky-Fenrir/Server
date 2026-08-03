CREATE PROCEDURE runtime.usp_GuildStateRelay_Publish @Kind TINYINT,
                                                     @SourceShardId TINYINT,
                                                     @GuildId INT,
                                                     @TargetCharacterId INT NULL,
                                                     @NewGuildId INT NULL,
                                                     @GuildName NVARCHAR(12),
                                                     @GuildRoleDb TINYINT,
                                                     @GuildCallName NVARCHAR(4),
                                                     @BuffType INT,
                                                     @BuffActive BIT,
                                                     @CorrelationId UNIQUEIDENTIFIER
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE @ExistingRelayId BIGINT = NULL;

    SELECT @ExistingRelayId = RelayId
    FROM runtime.GuildStateRelay
    WHERE CorrelationId = @CorrelationId;

    IF @ExistingRelayId IS NULL
        INSERT INTO runtime.GuildStateRelay
        (Kind, SourceShardId, GuildId, TargetCharacterId, NewGuildId, GuildName, GuildRoleDb, GuildCallName,
         BuffType, BuffActive, CorrelationId, CreatedAtUtc)
        VALUES (@Kind, @SourceShardId, @GuildId, @TargetCharacterId, @NewGuildId, @GuildName, @GuildRoleDb,
                @GuildCallName, @BuffType, @BuffActive, @CorrelationId, SYSUTCDATETIME());
END;
