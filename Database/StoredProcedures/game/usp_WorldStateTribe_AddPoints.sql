-- database/50_procedures/game/usp_WorldStateTribe_AddPoints.sql
-- Additive at the DB, unlike usp_WorldStateTribe_Update's whole-row overwrite: concurrent shards' deltas sum
-- correctly regardless of interleaving. AddTribePoints (WorldStateService.cs) is the one WorldState mutator
-- genuinely meant to be written by RvR kill-processing running on any map, i.e. potentially every shard
-- simultaneously (ADR-0012: every OTHER WorldState/WorldStateTribes field has exactly one live writer
-- cluster-wide at a time, since a map is hosted by exactly one shard). Idempotent-on-missing-row like
-- usp_WorldStateTribe_Update: a missing row (tribe never initialized) affects 0 rows and does not error.
CREATE PROCEDURE game.usp_WorldStateTribe_AddPoints @TribeId TINYINT,
                                                    @Delta INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.WorldStateTribes
    SET Points = Points + @Delta
    WHERE TribeId = @TribeId;
END;
