-- Fourth-tribe (Fujin) conversion/return (CZ_CHANGE_TO_TRIBE4_SEND, op37) success -- atomically writes
-- game.Characters.Tribe plus the 5-slot inline quest state (game.CharacterQuests), mirroring
-- usp_CharacterQuest_ApplyTransition's own upsert shape for that half. Every precondition (feature toggle,
-- level, tribe-point standings, quota, role/guild/friend gates, etc.) is already evaluated server-side by
-- Fenrir.Application.Game.Domain.Tribes.TribeMigrationGate before this procedure is ever called -- this
-- procedure only re-validates @NewTribe's own range as a last-line defensive guard, never re-derives
-- eligibility.
--
-- Distinct from, and must never be conflated with, game.usp_Character_ApplyTribeConversion (the unrelated
-- Book of Noble Dragon/Royal Serpent/Grand Tiger V2 skill-book mechanic, which DOES remap equipment/skills
-- between tribes 0/1/2): this procedure never touches money, items, equipment, or skills -- only tribe
-- membership and the 5 quest-progress columns, matching the "Fourth-tribe (Fujin) conversion and return"
-- behavior contract's own explicit side-effect scope.
--
-- DB-checked preconditions (THROW on failure): CharacterId exists (50334); @NewTribe is in the legal 0-3
-- range (50335).
--
-- Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:7565-7758 (full handler, side effects at :7739-7756) per the
-- "Fourth-tribe (Fujin) conversion and return" behavior contract.
CREATE PROCEDURE game.usp_Character_ApplyTribeFourConversion @CharacterId INT,
                                                              @NewTribe TINYINT,
                                                              @StepPermanent INT,
                                                              @ActiveQuestId INT,
                                                              @QSort INT,
                                                              @TargetPhase INT,
                                                              @KillCounter INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @NewTribe > 3
        THROW 50335, N'usp_Character_ApplyTribeFourConversion: NewTribe is outside the legal 0-3 range.', 1;

    BEGIN TRANSACTION;

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
        INSERT INTO game.CharacterQuests (CharacterId, StepPermanent, ActiveQuestId, QSort, TargetPhase, KillCounter)
        VALUES (@CharacterId, @StepPermanent, @ActiveQuestId, @QSort, @TargetPhase, @KillCounter);

    COMMIT TRANSACTION;
END;
