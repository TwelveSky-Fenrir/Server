-- Rare, event-driven persistence write for a single character's mount progression state (op87 mount lifecycle:
-- Convert/Delete/Transfer rolled attributes, delete-mount slot clear, inter-tribe-kill mount experience, and
-- mount-activity potion feeds). Legacy persists a single active mount (slot 0) as two packed integers plus the
-- selection/rental fields:
--   * MountExpActivity = activity * 1,000,000 + experience   (aAnimalExpActivity[0]; see MountActivityExpCodec)
--   * MountPower        = eight decimal attribute digits       (aAnimalPower[0];       see MountPowerCodec)
-- The runtime keeps both halves decoded on PlayerRuntimeState; the caller re-encodes via those codecs before
-- calling this. All five mount columns are written together so the whole mount snapshot commits atomically,
-- matching the legacy "persisted through the normal save path as one avatar record" shape. Not a per-tick
-- write -- invoked only on the discrete progression events above, the same posture as usp_Character_SetPetGrowth.
CREATE PROCEDURE game.usp_Character_SetMountProgression @CharacterId     INT,
                                                        @MountItemId     INT,
                                                        @MountExpActivity INT,
                                                        @MountPower      INT,
                                                        @MountSlotIndex  INT,
                                                        @MountTime       INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.Characters
    SET MountItemId      = @MountItemId,
        MountExpActivity = @MountExpActivity,
        MountPower       = @MountPower,
        MountSlotIndex   = @MountSlotIndex,
        MountTime        = @MountTime,
        UpdatedAtUtc     = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;
END;
