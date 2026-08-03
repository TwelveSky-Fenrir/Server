CREATE PROCEDURE game.usp_CharacterHotkeys_UpsertSlot @CharacterId INT,
                                                      @Page TINYINT,
                                                      @KeyIndex TINYINT,
                                                      @Sort INT,
                                                      @Value1 INT,
                                                      @Value2 INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DELETE
    FROM game.CharacterHotkeys
    WHERE CharacterId = @CharacterId
      AND Page = @Page
      AND KeyIndex = @KeyIndex;

    IF @Value2 <> 0
        BEGIN
            INSERT INTO game.CharacterHotkeys (CharacterId, Page, KeyIndex, Sort, Value1, Value2)
            VALUES (@CharacterId, @Page, @KeyIndex, @Sort, @Value1, @Value2);
        END;
END;
