-- database/50_procedures/game/usp_CharacterItems_ReplaceTwoContainers.sql
-- Contract: whole-container replace of TWO of a character's item containers in ONE transaction --
-- the cross-container twin of usp_CharacterItems_ReplaceContainer, for a single client move whose FROM
-- and TO slots live in different containers (e.g. equip: InventoryPage0 -> Equipment).
-- WHY THIS PROC EXISTS (review finding, Phase C/V2 integration): CzProcessDataSendHandler originally
-- called usp_CharacterItems_ReplaceContainer TWICE for a cross-container move -- two independently
-- committed transactions. If the FIRST call committed (source container emptied/updated) and the
-- SECOND then failed (transient SQL error, timeout, connection drop, cancellation), the item was
-- durably removed from the source and never durably added to the destination: a silent, permanent item
-- loss, violating D7 regime (b) ("jamais de fenetre de perte sur de la valeur") the same way a single
-- half-applied DELETE+INSERT would. This proc closes that window by wrapping BOTH containers' DELETE+
-- INSERT pairs in one BEGIN TRANSACTION/COMMIT, exactly like the single-container proc already does for
-- its own pair.
-- Params:
--   @CharacterId  INT
--   @ContainerA   TINYINT -- 0=InventoryPage0, 1=InventoryPage1, 2=Equipment, 3=StorePage0, 4=StorePage1
--   @ItemsA       game.tvp_CharacterItemSlot READONLY
--   @ContainerB   TINYINT -- must differ from @ContainerA (a same-container move never needs this proc)
--   @ItemsB       game.tvp_CharacterItemSlot READONLY
-- Result set: none.
-- Idempotent: yes -- replaying the same @ItemsA/@ItemsB is a no-op in effect.
-- DELETE-then-INSERT per container, never MERGE (architecture reference §12.3); XACT_ABORT ON guarantees
-- any error (including a CK/FK violation on either INSERT) rolls the WHOLE four-statement group back --
-- never a source-emptied-but-destination-unwritten half-state.
-- Errors:
--   THROW 50260 -- @ContainerA equals @ContainerB (caller error: use usp_CharacterItems_ReplaceContainer
--     for a same-container move instead).
CREATE PROCEDURE game.usp_CharacterItems_ReplaceTwoContainers
    @CharacterId INT,
    @ContainerA  TINYINT,
    @ItemsA      game.tvp_CharacterItemSlot READONLY,
    @ContainerB  TINYINT,
    @ItemsB      game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ContainerA = @ContainerB
        THROW 50260, N'ContainerA and ContainerB must differ -- use usp_CharacterItems_ReplaceContainer for a same-container move.', 1;

    BEGIN TRANSACTION;

    DELETE FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @ContainerA;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                      Enchant, Combine, Refine, Socket,
                                      SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterId,
           @ContainerA,
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
    FROM @ItemsA;

    DELETE FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @ContainerB;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                      Enchant, Combine, Refine, Socket,
                                      SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterId,
           @ContainerB,
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
    FROM @ItemsB;

    COMMIT TRANSACTION;
END;
