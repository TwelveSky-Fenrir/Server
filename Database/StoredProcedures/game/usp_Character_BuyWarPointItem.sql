-- Atomic War-Point NPC-shop purchase (USE_WAR_POINT_SYSTEM branch of ProcessForNPCShopToInventory,
-- Server/ts25zone/S04_MyWork05.cpp:1863-1895,1948-1987). Debits @WarPointCost War-Points and whole-replaces
-- the destination inventory container in ONE transaction (all-or-nothing). War-Point sufficiency is enforced
-- server-side here (WHERE WarPoint >= @WarPointCost) -- an insufficient balance changes NOTHING and returns the
-- sentinel -1 as an ordinary result set (a SOFT rejection: the legacy explicitly does not disconnect for this,
-- S04_MyWork05.cpp:1867-1873), rather than THROWing, so "can't afford" never surfaces as a fault the caller
-- must disambiguate from a real DB error. Contribution-Point cost is NOT handled here: it is a write-behind
-- field mirrored through the tribe-progress channel (same split the ordinary NPC-buy path uses), and every live
-- catalogue row charges 0 CP anyway (WarPointSystem.h:79-140).
CREATE PROCEDURE game.usp_Character_BuyWarPointItem @CharacterId INT,
                                                     @WarPointCost INT,
                                                     @Container TINYINT,
                                                     @Items game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @Debited TABLE
                     (
                         WarPoint INT
                     );

    UPDATE game.Characters
    SET WarPoint     = WarPoint - @WarPointCost,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.WarPoint
        INTO @Debited
    WHERE CharacterId = @CharacterId
      AND @WarPointCost >= 0
      AND WarPoint >= @WarPointCost;

    IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            -- Insufficient War-Points (or unknown character): soft rejection, nothing changed.
            SELECT CAST(-1 AS INT) AS NewWarPoint;
            RETURN;
        END;

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

    SELECT WarPoint AS NewWarPoint
    FROM @Debited;

    COMMIT TRANSACTION;
END;
