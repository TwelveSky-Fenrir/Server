-- Fourth-tribe (Fujin) conversion/return (CZ_CHANGE_TO_TRIBE4_SEND, op37) success -- atomically writes
-- game.Characters.Tribe plus the 5-slot inline quest state (game.CharacterQuests), mirroring
-- usp_CharacterQuest_ApplyTransition's own upsert shape for that half. Every precondition (feature toggle,
-- level, tribe-point standings, quota, role/guild/friend gates, etc.) is already evaluated server-side by
-- Fenrir.Application.Game.Domain.Tribes.TribeMigrationGate before this procedure is ever called -- this
-- procedure only re-validates @NewTribe's own range as a last-line defensive guard, never re-derives
-- eligibility.
--
-- @ConsumeQuota (default 0) additionally consumes one slot of the shared, server-wide daily
-- admin.TribeFourQuota inside THIS SAME transaction when set to 1 -- closes the transaction-composition-audit
-- finding that TribeMigrationService.ConvertAsync previously consumed that quota via a wholly separate,
-- uncoordinated call to admin.usp_TribeFourQuota_TryConsume: a crash/timeout between that call and this one
-- could silently burn one of the scarce server-wide daily slots without ever converting the character,
-- degrading the feature for every OTHER player converting that day. The guarded UPDATE below inlines
-- admin.usp_TribeFourQuota_TryConsume's own atomic check-and-increment (WHERE CurrentCount < MaxCount)
-- rather than EXEC-ing that procedure and capturing its SELECT-shaped Granted result, which would need an
-- INSERT ... EXEC round trip with no precedent anywhere in this codebase -- an inlined guarded UPDATE matches
-- this repo's own established check-then-act idiom (see usp_Character_AdjustMoney's money-cap guard).
-- Fenrir.Application.Game.Domain.Inventory.UseItems.ForcedNeutralTribeResetUseItemHandler (op23, item 8100)
-- reuses this same procedure for its own forced reset-to-neutral-tribe effect and always passes
-- @ConsumeQuota = 0 -- that feature has never been gated by the shared Fujin quota and must not start being
-- gated by it now.
--
-- Distinct from, and must never be conflated with, game.usp_Character_ApplyTribeConversion (the unrelated
-- Book of Noble Dragon/Royal Serpent/Grand Tiger V2 skill-book mechanic, which DOES remap equipment/skills
-- between tribes 0/1/2): this procedure never touches money, items, equipment, or skills -- only tribe
-- membership and the 5 quest-progress columns, matching the "Fourth-tribe (Fujin) conversion and return"
-- behavior contract's own explicit side-effect scope.
--
-- DB-checked preconditions (THROW on failure): CharacterId exists (50334); @NewTribe is in the legal 0-3
-- range (50335); @ConsumeQuota = 1 and the shared daily quota is already exhausted (50355).
--
-- The game.CharacterQuests half is an UPDATE-then-INSERT-if-0-rows upsert (no row = quest chain never
-- touched yet, per that table's own remarks) with the same TOCTOU shape as
-- game.usp_CharacterLogoutState_Upsert: two concurrent calls for the SAME @CharacterId that both see the
-- UPDATE affect 0 rows would both attempt the INSERT, and the loser hits PK_CharacterQuests. Because that
-- INSERT sits inside this proc's own explicit BEGIN TRANSACTION alongside the TribeFourQuota consume and the
-- Characters.Tribe write, catching the resulting constraint violation cannot just retry the CharacterQuests
-- half alone -- under XACT_ABORT ON the violation dooms the WHOLE transaction (XACT_STATE() = -1, verified
-- against Microsoft Learn's TRY...CATCH/XACT_STATE docs), which would silently discard the already-applied
-- quota consume and Tribe change too if left uncorrected. The CATCH therefore ROLLBACK TRANSACTIONs (the
-- only operation a doomed transaction still permits) and replays every write in a fresh transaction,
-- including a fresh @ConsumeQuota check (a genuinely new attempt against whatever admin.TribeFourQuota looks
-- like now, so a 50355 THROW on replay is a legitimate exhausted-in-the-meantime outcome, not a bug); the
-- Characters existence check is not re-run on replay since it already succeeded once in the failed attempt,
-- and the CharacterQuests row now exists (the winner's INSERT committed), so the replayed UPDATE
-- deterministically lands this caller's own quest-state values.
--
-- Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:7565-7758 (full handler, side effects at :7739-7756) per the
-- "Fourth-tribe (Fujin) conversion and return" behavior contract.
CREATE PROCEDURE game.usp_Character_ApplyTribeFourConversion @CharacterId INT,
                                                             @NewTribe TINYINT,
                                                             @StepPermanent INT,
                                                             @ActiveQuestId INT,
                                                             @QSort INT,
                                                             @TargetPhase INT,
                                                             @KillCounter INT,
                                                             @ConsumeQuota BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @NewTribe > 3
        THROW 50335, N'usp_Character_ApplyTribeFourConversion: NewTribe is outside the legal 0-3 range.', 1;

    BEGIN TRANSACTION;

    IF @ConsumeQuota = 1
        BEGIN
            UPDATE admin.TribeFourQuota
            SET CurrentCount = CurrentCount + 1
            WHERE Id = 1
              AND CurrentCount < MaxCount;

            IF @@ROWCOUNT = 0
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 50355,
                        N'usp_Character_ApplyTribeFourConversion: shared daily fourth-tribe quota exhausted.', 1;
                END;
        END;

    UPDATE game.Characters
    SET Tribe        = @NewTribe,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;

    IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50334, N'usp_Character_ApplyTribeFourConversion: unknown CharacterId.', 1;
        END;

    UPDATE game.CharacterQuests
    SET StepPermanent = @StepPermanent,
        ActiveQuestId = @ActiveQuestId,
        QSort         = @QSort,
        TargetPhase   = @TargetPhase,
        KillCounter   = @KillCounter
    WHERE CharacterId = @CharacterId;

    IF @@ROWCOUNT = 0
    BEGIN
        BEGIN TRY
            INSERT INTO game.CharacterQuests (CharacterId, StepPermanent, ActiveQuestId, QSort, TargetPhase, KillCounter)
            VALUES (@CharacterId, @StepPermanent, @ActiveQuestId, @QSort, @TargetPhase, @KillCounter);
        END TRY
        BEGIN CATCH
            IF ERROR_NUMBER() NOT IN (2627, 2601)
                THROW;

            ROLLBACK TRANSACTION;

            BEGIN TRANSACTION;

            IF @ConsumeQuota = 1
                BEGIN
                    UPDATE admin.TribeFourQuota
                    SET CurrentCount = CurrentCount + 1
                    WHERE Id = 1
                      AND CurrentCount < MaxCount;

                    IF @@ROWCOUNT = 0
                        BEGIN
                            ROLLBACK TRANSACTION;
                            THROW 50355,
                                N'usp_Character_ApplyTribeFourConversion: shared daily fourth-tribe quota exhausted (retry).', 1;
                        END;
                END;

            UPDATE game.Characters
            SET Tribe        = @NewTribe,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE CharacterId = @CharacterId;

            UPDATE game.CharacterQuests
            SET StepPermanent = @StepPermanent,
                ActiveQuestId = @ActiveQuestId,
                QSort         = @QSort,
                TargetPhase   = @TargetPhase,
                KillCounter   = @KillCounter
            WHERE CharacterId = @CharacterId;

            COMMIT TRANSACTION;

            RETURN;
        END CATCH;
    END;

    COMMIT TRANSACTION;
END;
