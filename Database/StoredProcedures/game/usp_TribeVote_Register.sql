-- database/50_procedures/game/usp_TribeVote_Register.sql
-- CZ_TRIBE_VOTE_SEND tSort 1, TRIBE_VOTE_V2 branch (S04_MyWork02.cpp:11610+): registers a Force Leader
-- candidate into the caller-chosen slot, displacing whoever is currently there. The application layer has
-- already verified the displacement is legal (challenger's KillOtherTribeCount strictly exceeds the
-- incumbent's, or the slot is empty) and that this character is not already registered in a different
-- slot for this tribe -- this proc only performs the upsert itself.
--
-- Concurrency fix (2026-07-12 hardening pass): the original EXISTS-check-then-UPDATE-or-INSERT shape had the
-- same unguarded upsert race as usp_HeroRanking_AddPoints had before this same pass fixed it -- two different
-- candidates registering into the SAME still-empty (TribeId, SlotIndex) slot at nearly the same instant could
-- both see "no row yet" and both fall into the INSERT branch, with the second losing to a PK_TribeVotes
-- violation instead of the deterministic "whoever's write lands second wins the slot" outcome the legacy
-- unconditional insert/update implies. WITH (UPDLOCK, HOLDLOCK) on the existence check (same idiom as
-- usp_GuildMember_Add's member-count check / usp_HeroRanking_Rollover's singleton-row check) takes a key-range
-- lock scoped to this exact (TribeId, SlotIndex) pair, serializing only concurrent registrations for the SAME
-- slot -- registrations for other slots/tribes are never blocked by this.
CREATE PROCEDURE game.usp_TribeVote_Register @TribeId TINYINT,
                                             @SlotIndex TINYINT,
                                             @CandidateCharacterId INT,
                                             @CandidateLevel SMALLINT,
                                             @KillOtherTribeCount INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    BEGIN TRANSACTION;

    IF
        EXISTS (SELECT 1 FROM game.TribeVotes WITH (UPDLOCK, HOLDLOCK) WHERE TribeId = @TribeId AND SlotIndex = @SlotIndex)
        UPDATE game.TribeVotes
        SET CandidateCharacterId = @CandidateCharacterId,
            CandidateLevel       = @CandidateLevel,
            KillOtherTribeCount  = @KillOtherTribeCount,
            VotePoint            = 0,
            RegisteredAtUtc      = SYSUTCDATETIME()
        WHERE TribeId = @TribeId
          AND SlotIndex = @SlotIndex;
    ELSE
        INSERT INTO game.TribeVotes (TribeId, SlotIndex, CandidateCharacterId, CandidateLevel, KillOtherTribeCount,
                                     VotePoint)
        VALUES (@TribeId, @SlotIndex, @CandidateCharacterId, @CandidateLevel, @KillOtherTribeCount, 0);

    COMMIT TRANSACTION;
END;
