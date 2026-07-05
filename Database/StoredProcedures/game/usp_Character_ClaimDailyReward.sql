-- Legacy resets the day counter every Monday via a scheduled tick; this proc recomputes the same reset
-- lazily from the stored RewardClaimDate instead (see usp_Character_GetRewardClaimState).
CREATE PROCEDURE game.usp_Character_ClaimDailyReward @CharacterId INT,
    @TodayDate   INT,
    @Container   TINYINT,
    @Items       game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    DECLARE
@TodayAsDate DATE = TRY_CONVERT(DATE, CAST(@TodayDate AS VARCHAR(8)), 112);

BEGIN
TRANSACTION;

    -- Read-then-branch is safe: the real invariant is the guarded UPDATE's WHERE below, and concurrent
    -- claims for the same character already serialize at the app layer.
    DECLARE
@IsNewWeek BIT;
SELECT @IsNewWeek = CASE
                        WHEN RewardClaimDate = 0 THEN 0
                        WHEN DATEDIFF(DAY, 0, @TodayAsDate) / 7 <>
                             DATEDIFF(DAY, 0, TRY_CONVERT(DATE, CAST(RewardClaimDate AS VARCHAR(8)), 112)) / 7
                            THEN 1
                        ELSE 0
    END
FROM game.Characters
WHERE CharacterId = @CharacterId;

UPDATE game.Characters
SET RewardClaimDay  = IIF(@IsNewWeek = 1, 1, RewardClaimDay + 1),
    RewardClaimDate = @TodayDate,
    UpdatedAtUtc    = SYSUTCDATETIME()
WHERE CharacterId = @CharacterId
  AND RewardClaimDate <> @TodayDate
  AND (RewardClaimDay BETWEEN 0 AND 6 OR @IsNewWeek = 1);

IF
@@ROWCOUNT = 0
        THROW 50270, N'Daily reward already claimed today, fully claimed, or unknown character.', 1;

DELETE
FROM game.CharacterItems
WHERE CharacterId = @CharacterId
  AND Container = @Container;

INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                 Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
SELECT @CharacterId,
       @Container,
       Slot,
       ItemId,
       Quantity,
       Enchant,
       Combine,
       Refine,
       Socket,
       SocketGem1,
       SocketGem2,
       SocketGem3,
       ExpireDate,
       Serial
FROM @Items;

COMMIT TRANSACTION;
END;
