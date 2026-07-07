-- game.Characters.Zone241Time -- the "aZone241Time" counter (see Migrations/041_character_rebirth_zone241_time.sql
-- for the column's own citation/header). First durable consumer: the Rebirth-advancement behavior contract's
-- Path B ("Max Rebirth", CZ_TRIBE_WORK_SEND tSort 11) success branch, which increments this by 10
-- (Server/ts25zone/S04_MyWork02.cpp:11342-11390) -- whether that increment should be applied unconditionally
-- on passing preconditions (the legacy's own quirky behavior) or only on an actual successful rebirth
-- transition (the contract's hardening recommendation) is a decision the calling domain code makes; this
-- procedure is a dumb delta-adjust primitive, the same shape as usp_Character_AdjustDeathProtection /
-- usp_Character_GrantTribeTransferPermit. @Delta's sign is not hard-coded, same forward-compat posture as
-- those two siblings -- no known legacy decrement path exists yet, but nothing here assumes increment-only.
-- Lower bound (>= 0, matching CK_Characters_Zone241Time) is folded into the guarded UPDATE, not a separate
-- pre-check, closing the same TOCTOU window usp_Character_AdjustMoney's own header describes.
-- Throws 50336 on an unknown character or an adjustment that would take Zone241Time negative. Returns the
-- post-adjustment value so a caller can mirror it to the client without a second round trip.
CREATE PROCEDURE game.usp_Character_AdjustZone241Time @CharacterId INT,
                                                       @Delta INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Adjusted TABLE
                      (
                          Zone241Time INT
                      );

    UPDATE game.Characters
    SET Zone241Time = Zone241Time + @Delta,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.Zone241Time INTO @Adjusted
    WHERE CharacterId = @CharacterId
      AND Zone241Time + @Delta >= 0;

    IF @@ROWCOUNT = 0
        THROW 50336, N'Unknown character or insufficient Zone241Time balance for this adjustment.', 1;

    SELECT Zone241Time AS NewZone241Time FROM @Adjusted;
END;
