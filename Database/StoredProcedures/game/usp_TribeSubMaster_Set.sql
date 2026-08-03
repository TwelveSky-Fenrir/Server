CREATE PROCEDURE game.usp_TribeSubMaster_Set @TribeId TINYINT,
                                             @SlotIndex TINYINT,
                                             @CharacterId INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        EXISTS (SELECT 1 FROM game.TribeSubMasters WHERE TribeId = @TribeId AND SlotIndex = @SlotIndex)
        THROW 50310, N'Tribe sub-master slot is already occupied.', 1;

    IF
        EXISTS (SELECT 1 FROM game.TribeSubMasters WHERE TribeId = @TribeId AND CharacterId = @CharacterId)
        THROW 50311, N'Character already holds a sub-master slot in this tribe.', 1;

    INSERT INTO game.TribeSubMasters (TribeId, SlotIndex, CharacterId)
    VALUES (@TribeId, @SlotIndex, @CharacterId);
END;
