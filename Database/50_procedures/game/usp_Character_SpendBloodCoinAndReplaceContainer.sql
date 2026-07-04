-- database/50_procedures/game/usp_Character_SpendBloodCoinAndReplaceContainer.sql
-- Contract: atomically (1) debit BloodCoin and (2) whole-container replace ONE item container --
-- CZ_BUY_BLOOD_MARK_SEND (contracts/04_commerce.md, USE_BLOOD family), D7 regime (b). Same shape as
-- usp_Character_AdjustMoneyAndReplaceContainer, scoped to the BloodCoin column instead of Money/BigMoney
-- (blood-mark exchange never touches ordinary money -- verified S04_MyWork02.cpp:15274-15371).
-- Params:
--   @CharacterId    INT
--   @DeltaBloodCoin INT      -- always <= 0 for a purchase (spend); no known legacy grant path exists in
--                                the source available to this investigation (see game.Characters.BloodCoin
--                                header comment) but the sign is not hard-coded here for forward compat.
--   @Container      TINYINT
--   @Items          game.tvp_CharacterItemSlot READONLY -- the container's FULL new content
-- Result set (RS0, single row/column): NewBloodCoin INT -- the post-debit balance, so the caller can echo
-- ZC_BUY_BLOOD_MARK_RECV.BloodCoin without a second read.
-- Idempotent: no (same posture as usp_Character_AdjustMoney).
-- Single guarded UPDATE (BloodCoin + @DeltaBloodCoin >= 0), not read-then-write -- same TOCTOU rationale as
-- usp_Character_AdjustMoney's own header comment.
-- Errors:
--   THROW 50271 -- the adjustment would drive BloodCoin negative, or @CharacterId does not exist
--                  (admin.ErrorCatalog, 502xx = game range).
CREATE PROCEDURE game.usp_Character_SpendBloodCoinAndReplaceContainer
    @CharacterId    INT,
    @DeltaBloodCoin INT,
    @Container      TINYINT,
    @Items          game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @Debited TABLE (BloodCoin INT);

    UPDATE game.Characters
    SET BloodCoin    = BloodCoin + @DeltaBloodCoin,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.BloodCoin
    INTO @Debited
    WHERE CharacterId = @CharacterId
      AND BloodCoin + @DeltaBloodCoin >= 0;

    IF @@ROWCOUNT = 0
        THROW 50271, N'Unknown character or insufficient BloodCoin balance for this adjustment.', 1;

    DELETE FROM game.CharacterItems WHERE CharacterId = @CharacterId AND Container = @Container;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                      Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterId, @Container, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
           SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial
    FROM @Items;

    SELECT BloodCoin AS NewBloodCoin FROM @Debited;

    COMMIT TRANSACTION;
END;
