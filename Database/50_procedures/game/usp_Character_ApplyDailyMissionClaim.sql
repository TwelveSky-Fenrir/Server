-- database/50_procedures/game/usp_Character_ApplyDailyMissionClaim.sql
-- Contract: atomically (1) write the 4 daily-mission counters (game.Characters.JoinWar/
-- MissionKillOtherTribe/MissionKillMonster/MissionPlayTime, wAvatar.aMissionDate) AFTER deduction, and
-- (2) OPTIONALLY whole-container-replace ONE inventory container to deposit the claimed reward item --
-- verified S04_MyWork02.cpp:14521 (W_MISSION_COMPLETE_SEND, tSort=2): the counters are only ever
-- decremented alongside a successful SendItemToInventory in the SAME logical action.
-- Params:
--   @CharacterId INT
--   @JoinWar INT, @KillOtherTribe INT, @KillMonster INT, @PlayTime INT -- the 4 counters' FINAL values
--     (already deducted by the caller), always written.
--   @Container TINYINT = 255 -- sentinel (no valid container ever uses it) = do not touch any container;
--     otherwise 0=InventoryPage0, 1=InventoryPage1.
--   @Items game.tvp_CharacterItemSlot READONLY -- the container's FULL new content; consulted only when
--     @Container <> 255.
-- Result set: none.
-- Idempotent: no (replaying decrements the counters again) -- same posture as every other write proc in
-- this schema; the caller (single EconomyActionLock-serialized handler) never retries after commit.
CREATE PROCEDURE game.usp_Character_ApplyDailyMissionClaim
    @CharacterId    INT,
    @JoinWar        INT,
    @KillOtherTribe INT,
    @KillMonster    INT,
    @PlayTime       INT,
    @Container      TINYINT = 255,
    @Items          game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    UPDATE game.Characters
    SET JoinWar               = @JoinWar,
        MissionKillOtherTribe = @KillOtherTribe,
        MissionKillMonster    = @KillMonster,
        MissionPlayTime       = @PlayTime,
        UpdatedAtUtc          = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;

    IF @Container <> 255
    BEGIN
        DELETE FROM game.CharacterItems
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
