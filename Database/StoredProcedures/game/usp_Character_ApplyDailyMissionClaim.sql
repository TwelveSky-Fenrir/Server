CREATE PROCEDURE game.usp_Character_ApplyDailyMissionClaim @CharacterId INT,
                                                           @JoinWar INT,
                                                           @KillOtherTribe INT,
                                                           @KillMonster INT,
                                                           @PlayTime INT,
                                                           @Container TINYINT = 255,
                                                           @Items game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN
        TRANSACTION;

    UPDATE game.Characters
    SET JoinWar               = @JoinWar,
        MissionKillOtherTribe = @KillOtherTribe,
        MissionKillMonster    = @KillMonster,
        MissionPlayTime       = @PlayTime,
        UpdatedAtUtc          = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;

    IF
        @Container <> 255
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
