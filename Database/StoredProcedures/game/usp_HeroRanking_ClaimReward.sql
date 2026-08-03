CREATE PROCEDURE game.usp_HeroRanking_ClaimReward @CharacterId INT,
                                                  @PeriodKind TINYINT,
                                                  @ContributionPointsDelta INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN
        TRANSACTION;

    UPDATE game.HeroRankings
    SET RewardClaimed = 1
    WHERE CharacterId = @CharacterId
      AND PeriodKind = @PeriodKind
      AND (RewardClaimed = 0 OR RewardClaimed IS NULL);

    IF
        @@ROWCOUNT = 0
        THROW 50357, N'Hero-ranking reward already claimed, or no claimable ranking row for this character/period.', 1;

    UPDATE game.Characters
    SET ContributionPoints = ContributionPoints + @ContributionPointsDelta,
        UpdatedAtUtc       = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;

    IF
        @@ROWCOUNT = 0
        THROW 50358, N'Unknown character for hero-ranking reward contribution-points grant.', 1;

    COMMIT TRANSACTION;
END;
