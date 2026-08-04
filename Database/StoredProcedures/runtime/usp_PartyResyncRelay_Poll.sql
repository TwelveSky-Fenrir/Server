CREATE OR ALTER PROCEDURE runtime.usp_PartyResyncRelay_Poll @ShardId TINYINT,
                                                            @RetentionSeconds INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DECLARE @LastRelayId BIGINT = 0;

    SELECT @LastRelayId = LastRelayId
    FROM runtime.PartyResyncRelayCursor WITH (SNAPSHOT)
    WHERE ShardId = @ShardId;

    SELECT RelayId,
           Sort,
           SourceShardId,
           SourceCharacterId,
           PartyName,
           AvatarName,
           MemberId1,
           MemberName1,
           MemberId2,
           MemberName2,
           MemberId3,
           MemberName3,
           MemberId4,
           MemberName4,
           MemberId5,
           MemberName5,
           RecipientCharacterId,
           CorrelationId,
           RequestCorrelationId
    FROM runtime.PartyResyncRelay WITH (SNAPSHOT)
    WHERE RelayId > @LastRelayId
      AND SourceShardId <> @ShardId
    ORDER BY RelayId ASC;

END;
