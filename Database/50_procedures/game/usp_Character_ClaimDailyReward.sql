-- database/50_procedures/game/usp_Character_ClaimDailyReward.sql
-- Contract: atomically (1) advance the 7-day login-reward cycle one day and (2) whole-container replace
-- the ONE inventory container the granted item landed in -- CZ_CLAIM_REWARD_ITEM_SEND (contracts/
-- 04_commerce.md), D7 regime (b): the reward item is a value object, never write-behind.
-- Params:
--   @CharacterId INT
--   @TodayDate   INT     -- caller-supplied YYYYMMDD (app clock), same raw-int convention as
--                            OfflineShops.ShopDate; the guard below is the SAME idiom
--                            usp_Character_AdjustMoney uses for its own atomic invariant
--   @Container   TINYINT -- 0=InventoryPage0, 1=InventoryPage1 (the granted item never lands anywhere else)
--   @Items       game.tvp_CharacterItemSlot READONLY -- the container's FULL new content
-- Result set: none. The caller resolves WHICH item to grant from the day read via
-- usp_Character_GetRewardClaimState BEFORE calling this (world.RewardBundleItems is keyed by that same
-- day index) -- a race that loses (see guard below) fails this call cleanly before anything is written,
-- so a stale precomputed item is never actually persisted.
-- Idempotent: no (same posture as usp_Character_AdjustMoney -- a caller must not retry after a successful
-- commit).
-- Single guarded UPDATE (RewardClaimDate <> @TodayDate AND (RewardClaimDay BETWEEN 0 AND 6 OR a weekly
-- reset is due)) closes the SAME TOCTOU window usp_Character_AdjustMoney's own header comment warns about:
-- two concurrent claims for the same character must not both succeed for the same day, nor both advance
-- the day counter twice.
-- CORRECTION (review finding): the legacy resets mPLAYUSER.uRewardClaimDay to 0 every MONDAY via a
-- scheduled ts25center tick (Server/ts25center/S07_MyGame01.cpp:218-238) -- a prior pass here only ever
-- incremented the counter, so it never actually recurred weekly as the legacy does. See
-- usp_Character_GetRewardClaimState's own header for why this is computed LAZILY (from the already-stored
-- RewardClaimDate) rather than via a new scheduled background job, and for why DATEDIFF(DAY, 0, x) / 7 is
-- used instead of the DATEFIRST-dependent DATEDIFF(WEEK, 0, x): the former reliably buckets any date into
-- its Monday-to-Sunday week (SQL Server's day zero, 1900-01-01, is itself a Monday) regardless of session
-- settings. If @TodayDate falls in a different such bucket than the last recorded claim, this claim is
-- treated as day 0 of a fresh cycle (permitted even if RewardClaimDay was already at 7) and the counter is
-- written as if it had already reset before this claim's own +1.
-- Params:
--   @CharacterId INT
--   @TodayDate   INT     -- caller-supplied YYYYMMDD (app clock), same raw-int convention as
--                            OfflineShops.ShopDate; the guard below is the SAME idiom
--                            usp_Character_AdjustMoney uses for its own atomic invariant
--   @Container   TINYINT -- 0=InventoryPage0, 1=InventoryPage1 (the granted item never lands anywhere else)
--   @Items       game.tvp_CharacterItemSlot READONLY -- the container's FULL new content
-- Errors:
--   THROW 50270 -- already claimed today, the day counter is already at 7 with no weekly reset due, or
--                  unknown character (admin.ErrorCatalog, 502xx = game range).
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

    -- Read-then-branch on @IsNewWeek is safe here despite not being folded into the guarded UPDATE's own
    -- WHERE/SET: the actual concurrency-sensitive invariant ("not already claimed today") is still enforced
    -- entirely by the atomic `RewardClaimDate <> @TodayDate` guard below, and callers already serialize
    -- concurrent claims for the SAME character via PlayerRuntimeState.EconomyActionLock at the application
    -- layer -- this can only ever mis-bucket a claim landing EXACTLY on a Monday-boundary race, a low-stakes
    -- cosmetic edge case, never a double-claim or a lost item.
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
