CREATE PROCEDURE runtime.usp_GuildTribeBroadcastRelay_Publish @Kind TINYINT,
                                                              @SourceShardId TINYINT,
                                                              @GuildId INT NULL,
                                                              @Tribe TINYINT NULL,
                                                              @RoleField TINYINT,
                                                              @AvatarName NVARCHAR(13),
                                                              @Content NVARCHAR(61),
                                                              @HasItemLink BIT,
                                                              @ItemLinkIndex INT NULL,
                                                              @ItemLinkActivity INT NULL,
                                                              @ItemLinkValue INT NULL,
                                                              @ItemLinkSocket0 INT NULL,
                                                              @ItemLinkSocket1 INT NULL,
                                                              @ItemLinkSocket2 INT NULL,
                                                              @CorrelationId UNIQUEIDENTIFIER
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE @ExistingRelayId BIGINT = NULL;

    SELECT @ExistingRelayId = RelayId
    FROM runtime.GuildTribeBroadcastRelay
    WHERE CorrelationId = @CorrelationId;

    IF @ExistingRelayId IS NULL
        INSERT INTO runtime.GuildTribeBroadcastRelay
        (Kind, SourceShardId, GuildId, Tribe, RoleField, AvatarName, Content, HasItemLink,
         ItemLinkIndex, ItemLinkActivity, ItemLinkValue, ItemLinkSocket0, ItemLinkSocket1, ItemLinkSocket2,
         CorrelationId, CreatedAtUtc)
        VALUES (@Kind, @SourceShardId, @GuildId, @Tribe, @RoleField, @AvatarName, @Content, @HasItemLink,
                @ItemLinkIndex, @ItemLinkActivity, @ItemLinkValue, @ItemLinkSocket0, @ItemLinkSocket1,
                @ItemLinkSocket2, @CorrelationId, SYSUTCDATETIME());
END;
