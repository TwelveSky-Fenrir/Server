CREATE PROCEDURE game.usp_CharacterFriend_Add @CharacterId INT,
                                              @Slot TINYINT,
                                              @FriendCharacterId INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        EXISTS (SELECT 1 FROM game.CharacterFriends WHERE CharacterId = @CharacterId AND Slot = @Slot)
        THROW 50267, N'Friend slot is already occupied.', 1;

    INSERT INTO game.CharacterFriends (CharacterId, Slot, FriendCharacterId)
    VALUES (@CharacterId, @Slot, @FriendCharacterId);
END;
