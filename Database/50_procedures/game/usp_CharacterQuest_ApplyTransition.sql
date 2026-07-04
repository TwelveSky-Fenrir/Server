-- database/50_procedures/game/usp_CharacterQuest_ApplyTransition.sql
-- Contract: atomically (1) upsert game.CharacterQuests' single 5-int row (wAvatar.aQuestInfo[5], report 04
-- §5), (2) OPTIONALLY credit money (quest reward type 2, S04_MyWork02.cpp:7409-7414), and (3) OPTIONALLY
-- whole-container-replace UP TO TWO item containers in the SAME transaction -- a quest Complete can touch
-- BOTH inventory pages in one logical action (the reward-item deposit slot the client chose, verified
-- S04_MyWork02.cpp:7381-7401, AND the quest item DeleteQuestItem scans BOTH pages for and wipes, verified
-- S07_MyGame04.cpp:2246-2269 -- these can legitimately land on different pages). Exactly like
-- usp_Character_AdjustMoneyAndReplaceTwoContainers already does for a two-page IMPROVE_ITEM attempt (a
-- quest reward is the same class of economy action).
-- Params:
--   @CharacterId   INT
--   @StepPermanent INT, @ActiveQuestId INT, @QSort INT, @TargetPhase INT, @KillCounter INT
--     -- the 5 new aQuestInfo ints; always written (a no-op UPDATE if unchanged).
--   @DeltaMoney    BIGINT = 0     -- 0 = no money touch. IMPORTANT: unlike usp_Character_AdjustMoney, a cap
--     breach here is SILENTLY SKIPPED (Money left unchanged), NEVER THROWN -- verified
--     S04_MyWork02.cpp:7409 (`if (CheckOverMaximum(...)) break;`): the legacy quest-completion reward loop
--     treats an over-cap money reward as "don't grant this one slot", not a reason to abort the whole
--     quest completion (which still resets quest state / deletes the quest item / etc. regardless). This
--     is a DELIBERATE, verified divergence from every other money proc in this schema, which all hard-fail
--     on a cap breach.
--   @Container1/@Container2 TINYINT = 255 -- 255 (sentinel, no valid container ever uses it) = do not touch;
--     otherwise 0=InventoryPage0, 1=InventoryPage1 (quests never touch Equipment/Store). Passing the SAME
--     container id in both is the caller's error (undefined which write wins) -- never done by
--     QuestProgressHandler, which always merges same-container edits into ONE @Items list before calling.
--   @Items1/@Items2 game.tvp_CharacterItemSlot READONLY -- each container's FULL new content; consulted
--     only when its matching @ContainerN <> 255.
-- Result set: none.
-- Idempotent: no (same posture as usp_Character_AdjustMoney -- a caller must not retry after a successful
-- commit; the quest-state upsert itself IS idempotent in isolation, but @DeltaMoney is not).
-- No MERGE (forbidden, architecture reference §12.3): guarded UPDATE, falling back to INSERT only when no
-- row was touched, for the quest-state upsert; DELETE-then-INSERT for each container, same as
-- usp_CharacterItems_ReplaceContainer.
CREATE PROCEDURE game.usp_CharacterQuest_ApplyTransition
    @CharacterId   INT,
    @StepPermanent INT,
    @ActiveQuestId INT,
    @QSort         INT,
    @TargetPhase   INT,
    @KillCounter   INT,
    @DeltaMoney    BIGINT = 0,
    @Container1    TINYINT = 255,
    @Items1        game.tvp_CharacterItemSlot READONLY,
    @Container2    TINYINT = 255,
    @Items2        game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    UPDATE game.CharacterQuests
    SET StepPermanent = @StepPermanent,
        ActiveQuestId = @ActiveQuestId,
        QSort         = @QSort,
        TargetPhase   = @TargetPhase,
        KillCounter   = @KillCounter
    WHERE CharacterId = @CharacterId;

    IF @@ROWCOUNT = 0
        INSERT INTO game.CharacterQuests (CharacterId, StepPermanent, ActiveQuestId, QSort, TargetPhase, KillCounter)
        VALUES (@CharacterId, @StepPermanent, @ActiveQuestId, @QSort, @TargetPhase, @KillCounter);

    IF @DeltaMoney <> 0
        UPDATE game.Characters
        SET Money        = Money + @DeltaMoney,
            UpdatedAtUtc  = SYSUTCDATETIME()
        WHERE CharacterId = @CharacterId
          AND Money + @DeltaMoney BETWEEN 0 AND 2000000000;
        -- Deliberately NO error branch here -- see header comment: a cap breach (or a vanished character,
        -- which cannot really happen mid-session) silently leaves Money untouched rather than aborting the
        -- transaction, matching the verified legacy `break;` semantics exactly.

    IF @Container1 <> 255
    BEGIN
        DELETE FROM game.CharacterItems
        WHERE CharacterId = @CharacterId
          AND Container = @Container1;

        INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                          Enchant, Combine, Refine, Socket,
                                          SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
        SELECT @CharacterId, @Container1, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
               SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial
        FROM @Items1;
    END;

    IF @Container2 <> 255
    BEGIN
        DELETE FROM game.CharacterItems
        WHERE CharacterId = @CharacterId
          AND Container = @Container2;

        INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity,
                                          Enchant, Combine, Refine, Socket,
                                          SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
        SELECT @CharacterId, @Container2, Slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
               SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial
        FROM @Items2;
    END;

    COMMIT TRANSACTION;
END;
