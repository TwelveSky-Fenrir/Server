-- database/50_procedures/game/usp_OfflineShop_Upsert.sql
-- Contract: open a new offline shop or update an existing one's location/state/money/name (item slots are
-- set separately via game.usp_OfflineShop_SetItems, since the client submits its whole shop layout as one
-- batch, not per-slot -- see game.tvp_OfflineShopItemSlot).
-- Params:
--   @CharacterId INT           -- game.Characters.CharacterId
--   @ZoneNumber  SMALLINT NULL -- world.Zones.ZoneNumber
--   @ShopState   TINYINT
--   @ShopDate    INT           -- kept as the legacy raw YYYYMMDD-style int (see game.OfflineShops)
--   @Money       INT
--   @BigMoney    INT
--   @LocationX   INT
--   @LocationY   INT
--   @LocationZ   INT
--   @ShopName    NVARCHAR(48)  -- legacy-fidelity only; game.ProxyShopNames is the authoritative display name
-- Result set: none.
-- Idempotent: yes -- re-running with the same inputs leaves the row in the same state.
-- No MERGE (forbidden, architecture reference §12.3): a single guarded UPDATE, falling back to INSERT.
CREATE PROCEDURE game.usp_OfflineShop_Upsert @CharacterId INT,
    @ZoneNumber  SMALLINT = NULL,
    @ShopState   TINYINT,
    @ShopDate    INT,
    @Money       INT,
    @BigMoney    INT,
    @LocationX   INT,
    @LocationY   INT,
    @LocationZ   INT,
    @ShopName    NVARCHAR(48) = N''
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
        INSERT INTO game.OfflineShops (CharacterId, ZoneNumber, ShopState, ShopDate, Money, BigMoney, LocationX, LocationY, LocationZ, ShopName)
        VALUES (@CharacterId, @ZoneNumber, @ShopState, @ShopDate, @Money, @BigMoney, @LocationX, @LocationY, @LocationZ, @ShopName);
END;
