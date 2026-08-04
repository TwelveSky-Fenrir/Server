CREATE PROCEDURE runtime.usp_PartyResyncRelay_Publish @Sort TINYINT,
                                                      @SourceShardId TINYINT,
                                                      @SourceCharacterId INT,
                                                      @RecipientCharacterId INT,
                                                      @PartyName NVARCHAR(13),
                                                      @AvatarName NVARCHAR(13),
                                                      @CorrelationId UNIQUEIDENTIFIER,
                                                      @RequestCorrelationId UNIQUEIDENTIFIER,
                                                      @MemberId1 INT,
                                                      @MemberName1 NVARCHAR(13),
                                                      @MemberId2 INT,
                                                      @MemberName2 NVARCHAR(13),
                                                      @MemberId3 INT,
                                                      @MemberName3 NVARCHAR(13),
                                                      @MemberId4 INT,
                                                      @MemberName4 NVARCHAR(13),
                                                      @MemberId5 INT,
                                                      @MemberName5 NVARCHAR(13)
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE @ExistingRelayId BIGINT = NULL;

    SELECT @ExistingRelayId = RelayId
    FROM runtime.PartyResyncRelay
    WHERE CorrelationId = @CorrelationId;

    IF @ExistingRelayId IS NULL
        INSERT INTO runtime.PartyResyncRelay
        (Sort, SourceShardId, SourceCharacterId, RecipientCharacterId, PartyName, AvatarName,
         MemberId1, MemberName1, MemberId2, MemberName2, MemberId3, MemberName3,
         MemberId4, MemberName4, MemberId5, MemberName5,
         CorrelationId, RequestCorrelationId, CreatedAtUtc)
        VALUES (@Sort, @SourceShardId, @SourceCharacterId, @RecipientCharacterId, @PartyName, @AvatarName,
                @MemberId1, @MemberName1, @MemberId2, @MemberName2, @MemberId3, @MemberName3,
                @MemberId4, @MemberName4, @MemberId5, @MemberName5,
                @CorrelationId, @RequestCorrelationId, SYSUTCDATETIME());
END;
