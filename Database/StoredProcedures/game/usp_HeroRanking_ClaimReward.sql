-- Folds the hero-ranking reward claim's durable RewardClaimed flag and its contribution-points grant into
-- one atomic transaction. Previously HeroRewardClaimService called usp_HeroRanking_Upsert to durably set
-- RewardClaimed=1 and then granted the actual reward only through an in-memory PlayerRuntimeState mirror
-- (a zone command, later flushed by the write-behind progress batch) -- a crash or a full zone-command inbox
-- between those two steps could burn the one-time claim forever without the reward ever landing durably.
-- Mirrors usp_Character_ClaimDailyReward's own claim-state-plus-grant bundling.
-- The guarded first UPDATE also closes a concurrent-double-claim race usp_HeroRanking_Upsert never guarded
-- against (a blind upsert would happily re-set RewardClaimed=1 a second time for a racing caller).
-- Known residual gap, out of scope for this fix: game.Characters.ContributionPoints is still part of
-- game.tvp_CharacterProgress / usp_Character_PersistProgressBatch's last-write-wins write-behind snapshot
-- (unlike Money/BigMoney, which that procedure's own header explains were deliberately excluded for exactly
-- this reason). If the caller's in-memory PlayerRuntimeState mirror of this grant is dropped (e.g. a full
-- zone-command inbox) AND the character is later flushed for an unrelated progress change before its
-- in-memory ContributionPoints catches up, that later flush can silently revert this grant. Closing that
-- would mean giving ContributionPoints its own AdjustMoney-style atomic adjustor and removing it from the
-- progress batch across every CP-granting call site (PvP kills, quest rewards, etc.) -- a much larger change
-- than this specific claim-plus-grant atomicity fix, not attempted here.
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
