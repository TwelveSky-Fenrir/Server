-- database/50_procedures/game/usp_CharacterFriend_Add.sql
-- Contract: CZ_FRIEND_MAKE_SEND (opcode 56) -- writes ONE friend slot for the CALLING character only
-- (one-directional, see game.CharacterFriends' own header). The client picks @Slot itself
-- (contracts/05_social.md: "tIndex @0 = slot d'ami choisi cote client (0..9)"); app-side
-- (FriendRegistry) has already verified the negotiation reached the accepted state before calling this
-- -- this proc only enforces the two DB-level invariants a race could still violate.
-- Params:
--   @CharacterId       INT
--   @Slot              TINYINT (0-9, CK_CharacterFriends_Slot is the range backstop)
--   @FriendCharacterId INT
-- Result set: none.
-- Idempotent: no (a repeat call throws 50267 -- the slot is then already occupied).
-- Errors:
--   THROW 50267 -- @Slot is already occupied for @CharacterId (game.CharacterFriends, 502xx = game range).
CREATE PROCEDURE game.usp_CharacterFriend_Add @CharacterId       INT,
    @Slot              TINYINT,
    @FriendCharacterId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM game.CharacterFriends WHERE CharacterId = @CharacterId AND Slot = @Slot)
        THROW 50267, N'Friend slot is already occupied.', 1;

    INSERT INTO game.CharacterFriends (CharacterId, Slot, FriendCharacterId)
    VALUES (@CharacterId, @Slot, @FriendCharacterId);
END;
