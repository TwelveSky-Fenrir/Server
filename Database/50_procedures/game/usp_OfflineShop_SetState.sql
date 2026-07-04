-- database/50_procedures/game/usp_OfflineShop_SetState.sql
-- Contract: flip a character's offline shop ShopState -- CZ_END_PSHOP_SEND Sort=2 (contracts/
-- 04_commerce.md). Verified against the real close path (Server/ts25extra/S08_MyDB.cpp:768-795,
-- MyDB::CloseProxy): closing only sets aShopState=0, it does NOT clear items or money -- those stay
-- attached to the row, retrievable one-by-one (usp_OfflineShop_RetrieveItemAndReplaceContainer) /
-- withdrawable (usp_OfflineShop_WithdrawMoney) only once ShopState=0. This is why
-- usp_OfflineShop_Close (a full DELETE) is NOT used for the player-facing close action -- that proc
-- predates this verification pass and is reserved for administrative/account-deletion cleanup only.
-- Params: @CharacterId INT, @ShopState TINYINT (0=closed, 1=open).
-- Result set: none.
-- Idempotent: yes -- setting the same state twice, or on a character with no shop row at all, affects 0
-- or 1 rows and never errors.
CREATE PROCEDURE game.usp_OfflineShop_SetState @CharacterId INT,
    @ShopState   TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE game.OfflineShops
    SET ShopState = @ShopState
    WHERE CharacterId = @CharacterId;
END;
