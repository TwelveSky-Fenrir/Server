-- Durable, transactional rune-state write for CZ_RUNE_SYSTEM_SEND (op157) equip/unequip -- NOT the per-tick
-- write-behind flush. Rune changes are rare, player-initiated, one socket at a time, and each is atomic with
-- an inventory mutation (Server/ts25zone/S04_MyWork03.cpp:8162-8328): the item leaves inventory and enters the
-- socket (equip), or leaves the socket and re-enters inventory (unequip), with no intermediate state where it
-- exists in both or neither. This procedure preserves that atomicity by replacing the whole rune-socket array
-- AND the one paired inventory container together, in ONE transaction -- a fault can never commit half of it
-- and dupe (item in both socket and inventory) or lose (item in neither) the rune.
--
-- @Runes is the WHOLE 4-socket state (occupied sockets only). An all-empty state omits the TVP (0 rows) and
-- the DELETE alone clears the table -- "empty list = deliberate clear", same rule as
-- usp_CharacterItems_ReplaceContainer.
--
-- @Container = 255 (the caller's NoContainer sentinel; no real inventory container id is 255) skips the
-- inventory replace entirely -- e.g. a rune-only re-anchor with no paired item move. Any real container id
-- additionally replaces exactly that one container from @Items (also omitted, per the same empty-TVP rule,
-- when that container becomes empty).
CREATE PROCEDURE game.usp_Character_PersistRunes @CharacterId INT,
                                                 @Container TINYINT,
                                                 @Runes game.tvp_CharacterRuneSocket READONLY,
                                                 @Items game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN
        TRANSACTION;

    DELETE
    FROM game.CharacterRunes
    WHERE CharacterId = @CharacterId;

    INSERT INTO game.CharacterRunes (CharacterId, SocketIndex, RuneItemId, RuneStat)
    SELECT @CharacterId,
           SocketIndex,
           RuneItemId,
           RuneStat
    FROM @Runes;

    -- Optional paired inventory container replace (the item side of the socket<->inventory swap). 255 = skip.
    IF @Container <> 255
        BEGIN
            DELETE
            FROM game.CharacterItems
            WHERE CharacterId = @CharacterId
              AND Container = @Container;

            INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                             Enchant, Combine, Refine, Socket,
                                             SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
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
        END;

    COMMIT TRANSACTION;
END;
