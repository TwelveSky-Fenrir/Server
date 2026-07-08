-- Fix-forward, not an in-place edit: StoredProcedures/game/usp_OfflineShop_WithdrawMoney.sql is already
-- applied/journaled, so this corrective script CREATE OR ALTERs the same procedure name from a brand-new
-- file instead of touching the original (see Database/_manifest.txt's own header and the
-- sql-server-2025-stored-procedure-conventions skill: "never edit an already-applied script").
--
-- Gap this closes (shop-and-proxyshop legacy-parity audit, WithdrawProxyShopEarnings / opcode 110 /
-- CZ_SET_DEPUTY_PSHOP_MONEY_SEND): the behavior contract requires two distinct failure codes on
-- withdrawal -- result 3 for a stale-client mismatch OR an open/expired shop, and a separate result 4
-- ("nothing to withdraw") for a zero pending balance on both components, "rather than a silent no-op
-- success" (Server/ts25zone/S07_MyGame09.cpp:886-958, contract Edge cases). The original procedure threw
-- the same SQL error 50276 for both the zero-balance short-circuit and the guarded-UPDATE stale/not-closed
-- case, which made result 4 unreachable from WithdrawProxyShopEarningsService's catch handling (it mapped
-- every exception to result 3). This version throws a new, distinct 50340 for the zero-balance case,
-- leaving 50276 exclusively for stale-client-mismatch/not-closed/expired.
--
-- Also closes a second gap from the same audit: the contract additionally requires the shop be "closed and
-- not expired" before a withdrawal is allowed, but the original guarded UPDATE only checked ShopState = 0
-- and never consulted the shop's own ShopDate (rental expiration, a compact YYYYMMDD int -- see
-- Tables/game/OfflineShops.sql and Application/Fenrir.Application.Game.Domain/World/Zone.ProxyShops.cs's
-- own `entry.ShopDate < today` expiry check, which the added `ShopDate >= @TodayDate` guard mirrors: "not
-- expired" is the negation of that same comparison). @TodayDate is computed once in C# via
-- GameDate.Today() and passed in by the caller, matching usp_Character_ClaimDailyReward's existing
-- @TodayDate convention, rather than re-deriving "today" a second, possibly-inconsistent way inside T-SQL.
--
-- New parameter appended at the end of the existing signature per convention -- CaeriusNet's
-- StoredProcedureParametersBuilder binds by name, but existing parameters are never reordered.
CREATE OR ALTER PROCEDURE game.usp_OfflineShop_WithdrawMoney @CharacterId INT,
                                                             @ExpectedMoney INT,
                                                             @ExpectedBigMoney INT,
                                                             @TodayDate INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ExpectedMoney = 0 AND @ExpectedBigMoney = 0
        THROW 50340, N'Nothing to withdraw from this offline shop.', 1;

    BEGIN TRANSACTION;

    UPDATE game.OfflineShops
    SET Money    = 0,
        BigMoney = 0
    WHERE CharacterId = @CharacterId
      AND ShopState = 0
      AND ShopDate >= @TodayDate
      AND Money = @ExpectedMoney
      AND BigMoney = @ExpectedBigMoney;

    IF @@ROWCOUNT = 0
        THROW 50276, N'Offline shop is not closed, has expired, or its earnings no longer match the expected amounts.', 1;

    UPDATE game.Characters
    SET Money        = Money + @ExpectedMoney,
        BigMoney     = BigMoney + @ExpectedBigMoney,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId
      AND Money + @ExpectedMoney BETWEEN 0 AND 2000000000
      AND BigMoney + @ExpectedBigMoney BETWEEN 0 AND 999;

    IF @@ROWCOUNT = 0
        BEGIN
            -- Diagnostic re-read only; picks which error code to throw.
            IF EXISTS (SELECT 1
                       FROM game.Characters
                       WHERE CharacterId = @CharacterId
                         AND BigMoney + @ExpectedBigMoney > 999)
                THROW 50333, N'Crediting this offline shop''s BigMoney earnings would exceed the legacy BigMoney cap (MAX_NUMBER_SIZE2 = 999).', 1;

            THROW 50261, N'Withdrawal would exceed the legacy money cap (MAX_NUMBER_SIZE = 2,000,000,000).', 1;
        END;

    COMMIT TRANSACTION;
END;
