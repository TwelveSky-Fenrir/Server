-- game.Characters.ProtectForDeath delta adjustment -- the "aProtectForDeath" decrementing death-protection
-- shield counter (Server/ts25zone/S07_MyGame02.cpp:3443-3489, ProcessAttack04 "Monster -> Avatar"): each
-- qualifying death consumes exactly one charge (@Delta = -1) in place of that death's CP/XP penalty; see the
-- character-death-respawn-penalty legacy-behavior-translator contract for the full consume-vs-penalty
-- decision, which stays in the C# domain layer (this procedure only durably applies whichever adjustment the
-- caller already decided on). @Delta's sign is not hard-coded, for forward compat, same posture as
-- usp_Character_GrantTribeTransferPermit's @Delta / usp_Character_SpendBloodCoinAndReplaceContainer's
-- @DeltaBloodCoin -- a future GM-grant command or a protection-charge consumable item can reuse this same
-- primitive with a positive @Delta, on top of the fixed starting grant of 5
-- (Migrations/025_character_protect_for_death_grant.sql).
-- Lower bound (>= 0, matching CK_Characters_ProtectForDeath) is folded into the guarded UPDATE, not a
-- separate pre-check, closing the same TOCTOU window usp_Character_AdjustMoney's own header describes.
-- Throws 50332 on an unknown character or an adjustment that would take ProtectForDeath negative. Returns
-- the post-adjustment count so a caller consuming a charge can notify the defender of the new shield count
-- without a second round trip.
CREATE PROCEDURE game.usp_Character_AdjustDeathProtection @CharacterId INT,
                                                          @Delta INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Adjusted TABLE
                      (
                          ProtectForDeath INT
                      );

    UPDATE game.Characters
    SET ProtectForDeath = ProtectForDeath + @Delta,
        UpdatedAtUtc    = SYSUTCDATETIME()
    OUTPUT INSERTED.ProtectForDeath INTO @Adjusted
    WHERE CharacterId = @CharacterId
      AND ProtectForDeath + @Delta >= 0;

    IF @@ROWCOUNT = 0
        THROW 50332, N'Unknown character or insufficient ProtectForDeath balance for this adjustment.', 1;

    SELECT ProtectForDeath AS NewProtectForDeath FROM @Adjusted;
END;
