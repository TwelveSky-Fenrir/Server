-- database/50_procedures/game/usp_OfflineShop_Upsert.sql
-- ShopName here is legacy-fidelity only; game.ProxyShopNames holds the authoritative display name.
-- Guarded UPDATE-then-INSERT, no MERGE (architecture reference §12.3).
CREATE PROCEDURE game.usp_OfflineShop_Upsert @CharacterId INT,
                                             @ZoneNumber SMALLINT = NULL,
                                             @ShopState TINYINT,
                                             @ShopDate INT,
                                             @Money INT,
                                             @BigMoney INT,
                                             @LocationX INT,
                                             @LocationY INT,
                                             @LocationZ INT,
                                             @ShopName NVARCHAR(48) = N''
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.OfflineShops
    SET ZoneNumber = @ZoneNumber,
        ShopState  = @ShopState,
        ShopDate   = @ShopDate,
        Money      = @Money,
        BigMoney   = @BigMoney,
        LocationX  = @LocationX,
        LocationY  = @LocationY,
        LocationZ  = @LocationZ,
        ShopName   = @ShopName
    WHERE CharacterId = @CharacterId;

    IF
        @@ROWCOUNT = 0
        INSERT INTO game.OfflineShops (CharacterId, ZoneNumber, ShopState, ShopDate, Money, BigMoney, LocationX,
                                       LocationY, LocationZ, ShopName)
        VALUES (@CharacterId, @ZoneNumber, @ShopState, @ShopDate, @Money, @BigMoney, @LocationX, @LocationY, @LocationZ,
                @ShopName);
END;
