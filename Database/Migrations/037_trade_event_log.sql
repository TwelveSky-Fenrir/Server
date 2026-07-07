-- Player-to-player trade item/money audit logging -- closes the "EventLogCategory.Trade has zero writers"
-- critical finding (legacy-behavior-translator contract, 2026-07 round). Legacy logs every completed trade
-- item-by-item and per-side money delta via GL_615_TRADE_ITEM/GL_615_TRADE_ITEM2 (item) and
-- GL_616_TRADE_MONEY (money) from the trade-menu-lock commit branch (Server/ts25zone/S04_MyWork02.cpp:
-- 8899-9018, the live #else branch -- NEW_TRADE_SRC is unconditionally #define'd three lines above it, so
-- the #ifndef branch calling GL_615_TRADE_ITEM/GL_616_TRADE_MONEY directly is dead code, never compiled;
-- GL_615_TRADE_ITEM2 is the one actually called, and its body forwards verbatim into GL_615_TRADE_ITEM's own
-- record-formatting body -- see Server/ts25zone/UpperCom/S06_MyUpperCom05.cpp:451-490). Fenrir's own trade
-- commit is entirely inside usp_CharacterTrade_Execute (already applied, StoredProcedures/game/
-- usp_CharacterTrade_Execute.sql) with zero audit-insert statement -- this migration adds the missing
-- logging to the SAME transaction, following the exact precedent Migrations/014_guild_money_event_log.sql
-- already established (CREATE OR ALTER of an already-applied proc, `EXEC game.usp_EventLog_Insert` as one
-- more statement immediately before COMMIT, so the audit rows can never survive a rolled-back trade and a
-- committed trade can never be missing them).
--
-- New parameters (@TradedItemsA/@TradedItemsB/@OfferedMoneyA../@OfferedBigMoneyB) are appended strictly
-- after the existing parameter list, never inserted among it, per the append-only parameter rule. They
-- carry data the procedure did not previously need: @ItemsA0/@ItemsA1/@ItemsB0/@ItemsB1 are each side's
-- FULL post-trade container contents (a whole-container replace) and @DeltaMoneyA/@DeltaMoneyB are each
-- side's NET money delta -- neither reconstructs what was actually placed into the trade window when both
-- sides contribute money in the same trade (@DeltaMoneyA = SideB.Money - SideA.Money loses the individual
-- raw split). @TradedItemsA/@TradedItemsB are each side's finalized trade-window offer only (legacy
-- ST_TRADE_INFO, up to 8 slots, MAX_TRADE_SLOT_NUM = 8, Server/Header/Protocol/DEFINE.h:291), reusing
-- game.tvp_CharacterItemSlot's existing row shape (Slot here is just the trade-window slot index, unused by
-- this procedure beyond passthrough). @OfferedMoneyA/@OfferedBigMoneyA/@OfferedMoneyB/@OfferedBigMoneyB are
-- each side's own raw contributed money offer, all defaulted to 0/omitted so the existing
-- ICharacterRepository.ExecuteTradeAsync call site (Application/Fenrir.Application.Game.Services/Social/
-- TradeLockService.cs) keeps compiling and behaving exactly as before until it is updated to pass the real
-- trade-window data -- same forward-compat posture already used for CreateWithStarterKitAsync's
-- previousTribe parameter (Migrations/018_character_previous_tribe_and_mount_readpath.sql).
--
-- EventCode is an app-owned numbering scheme scoped under Category (game.EventLog's own doc comment) -- this
-- migration defines, within EventLogCategory.Trade = 0: 1 = item transfer (GL_615_TRADE_ITEM/ITEM2), 2 =
-- money transfer (GL_616_TRADE_MONEY), mirroring how EventLogCategory.GuildMoney already uses EventCode
-- 1/2/3 for create/upgrade/disband within its own category.
--
-- Item rows: one per occupied slot (ItemId > 0, matching legacy's own "not positive = empty slot" gate,
-- Server/ts25zone/S04_MyWork02.cpp:8987,8998) for each side, done as a single set-based INSERT per side
-- rather than a per-row EXEC usp_EventLog_Insert loop -- both land in the exact same table via the exact
-- same column list, and a TVP is naturally bulk-insertable; a cursor/loop would add no correctness and only
-- cost round trips for what is at most 8 rows. Enchant/Combine/Refine/Socket/SocketGem1-3/Serial have no
-- dedicated game.EventLog columns, so they ride in Payload as semicolon-separated key=value text, the same
-- free-form convention 014's own GuildId/AvatarName/Grade payload already uses.
--
-- Money rows: one per side, gated on that side's own contribution (@OfferedMoneyA > 0 OR @OfferedBigMoneyA
-- > 0) exactly matching legacy's own >0 gate (Server/ts25zone/S04_MyWork02.cpp:8992,9003) -- a side that
-- contributed zero of both money components gets no money audit row at all, independent of whether that
-- same side offered items. Uses EXEC game.usp_EventLog_Insert (single row) rather than a bulk INSERT since
-- there is at most one row per side here, matching 014's own money-log call shape exactly.
--
-- Outcome = 1 (success) is the only value ever logged here: usp_CharacterTrade_Execute already THROWs
-- (50268/50269) before this point on any failure, so there is nothing to log on a rejected attempt -- same
-- reasoning as 014's own Outcome=1-only note.
CREATE OR ALTER PROCEDURE game.usp_CharacterTrade_Execute @CharacterA INT,
                                                          @ItemsA0 game.tvp_CharacterItemSlot READONLY,
                                                          @ItemsA1 game.tvp_CharacterItemSlot READONLY,
                                                          @DeltaMoneyA BIGINT,
                                                          @DeltaBigMoneyA INT,
                                                          @CharacterB INT,
                                                          @ItemsB0 game.tvp_CharacterItemSlot READONLY,
                                                          @ItemsB1 game.tvp_CharacterItemSlot READONLY,
                                                          @DeltaMoneyB BIGINT,
                                                          @DeltaBigMoneyB INT,
                                                          @TradedItemsA game.tvp_CharacterItemSlot READONLY,
                                                          @TradedItemsB game.tvp_CharacterItemSlot READONLY,
                                                          @OfferedMoneyA BIGINT = 0,
                                                          @OfferedBigMoneyA INT = 0,
                                                          @OfferedMoneyB BIGINT = 0,
                                                          @OfferedBigMoneyB INT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    UPDATE game.Characters
    SET Money        = Money + @DeltaMoneyA,
        BigMoney     = BigMoney + @DeltaBigMoneyA,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterA
      AND Money + @DeltaMoneyA >= 0
      AND BigMoney + @DeltaBigMoneyA >= 0;

    IF @@ROWCOUNT = 0
        THROW 50268, N'Character A: unknown character or insufficient money balance for this trade.', 1;

    UPDATE game.Characters
    SET Money        = Money + @DeltaMoneyB,
        BigMoney     = BigMoney + @DeltaBigMoneyB,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterB
      AND Money + @DeltaMoneyB >= 0
      AND BigMoney + @DeltaBigMoneyB >= 0;

    IF @@ROWCOUNT = 0
        THROW 50269, N'Character B: unknown character or insufficient money balance for this trade.', 1;

    DELETE FROM game.CharacterItems WHERE CharacterId = @CharacterA AND Container = 0;
    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                     Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterA,
           0,
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
    FROM @ItemsA0;

    DELETE FROM game.CharacterItems WHERE CharacterId = @CharacterA AND Container = 1;
    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                     Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterA,
           1,
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
    FROM @ItemsA1;

    DELETE FROM game.CharacterItems WHERE CharacterId = @CharacterB AND Container = 0;
    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                     Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterB,
           0,
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
    FROM @ItemsB0;

    DELETE FROM game.CharacterItems WHERE CharacterId = @CharacterB AND Container = 1;
    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                     Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterB,
           1,
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
    FROM @ItemsB1;

    -- Audit logging (GL_615_TRADE_ITEM/GL_615_TRADE_ITEM2/GL_616_TRADE_MONEY parity) -- both characters are
    -- confirmed to exist by the two guarded UPDATEs above, so these SELECTs always resolve an account id.
    DECLARE @AccountA INT, @AccountB INT;
    SELECT @AccountA = AccountId FROM game.Characters WHERE CharacterId = @CharacterA;
    SELECT @AccountB = AccountId FROM game.Characters WHERE CharacterId = @CharacterB;

    -- Character A's offered items -> B. Empty/omitted @TradedItemsA (no rows with ItemId > 0) produces
    -- nothing, matching legacy's own empty-slot skip.
    INSERT INTO game.EventLog (EventCode, Category, ActorAccountId, ActorCharacterId, TargetAccountId,
                               TargetCharacterId, ItemId, Quantity, Outcome, Payload)
    SELECT 1, -- item transfer (GL_615_TRADE_ITEM/GL_615_TRADE_ITEM2)
           0, -- EventLogCategory.Trade
           @AccountA,
           @CharacterA,
           @AccountB,
           @CharacterB,
           ItemId,
           Quantity,
           1,
           CONCAT(N'Enchant=', Enchant, N';Combine=', Combine, N';Refine=', Refine, N';Socket=', Socket,
                  N';SocketGem1=', SocketGem1, N';SocketGem2=', SocketGem2, N';SocketGem3=', SocketGem3,
                  N';Serial=', Serial)
    FROM @TradedItemsA
    WHERE ItemId > 0;

    -- Character B's offered items -> A (symmetric).
    INSERT INTO game.EventLog (EventCode, Category, ActorAccountId, ActorCharacterId, TargetAccountId,
                               TargetCharacterId, ItemId, Quantity, Outcome, Payload)
    SELECT 1,
           0,
           @AccountB,
           @CharacterB,
           @AccountA,
           @CharacterA,
           ItemId,
           Quantity,
           1,
           CONCAT(N'Enchant=', Enchant, N';Combine=', Combine, N';Refine=', Refine, N';Socket=', Socket,
                  N';SocketGem1=', SocketGem1, N';SocketGem2=', SocketGem2, N';SocketGem3=', SocketGem3,
                  N';Serial=', Serial)
    FROM @TradedItemsB
    WHERE ItemId > 0;

    -- Character A's offered money -> B. No row at all when A contributed zero of both components.
    IF @OfferedMoneyA > 0 OR @OfferedBigMoneyA > 0
        EXEC game.usp_EventLog_Insert
             @EventCode = 2, -- money transfer (GL_616_TRADE_MONEY)
             @Category = 0, -- EventLogCategory.Trade
             @ActorAccountId = @AccountA,
             @ActorCharacterId = @CharacterA,
             @TargetAccountId = @AccountB,
             @TargetCharacterId = @CharacterB,
             @DeltaMoney = @OfferedMoneyA,
             @DeltaBigMoney = @OfferedBigMoneyA,
             @Outcome = 1;

    -- Character B's offered money -> A (symmetric).
    IF @OfferedMoneyB > 0 OR @OfferedBigMoneyB > 0
        EXEC game.usp_EventLog_Insert
             @EventCode = 2,
             @Category = 0,
             @ActorAccountId = @AccountB,
             @ActorCharacterId = @CharacterB,
             @TargetAccountId = @AccountA,
             @TargetCharacterId = @CharacterA,
             @DeltaMoney = @OfferedMoneyB,
             @DeltaBigMoney = @OfferedBigMoneyB,
             @Outcome = 1;

    COMMIT TRANSACTION;
END;
GO
