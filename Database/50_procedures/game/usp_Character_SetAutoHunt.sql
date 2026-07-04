-- database/50_procedures/game/usp_Character_SetAutoHunt.sql
-- Contract: persists the auto-hunt on/off flag and the raw 112-byte AUTO_HUNT config blob
-- (CZ_AUTO_CONFIG_SEND, W_AUTO_CONFIG_SEND, S04_MyWork02.cpp:13466) -- verified: the legacy CopyMemory's
-- the client-submitted struct verbatim with NO content validation, so this proc stores it as an opaque
-- VARBINARY(112) rather than decomposing it into columns (Fenrir.Contracts.Packets.Shared.AutoHunt.Write
-- already knows how to serialize/deserialize this exact 112-byte shape).
-- Params: @CharacterId INT, @Enabled BIT, @Config VARBINARY(112).
-- Result set: none. Idempotent: yes.
CREATE PROCEDURE game.usp_Character_SetAutoHunt @CharacterId INT,
    @Enabled     BIT,
    @Config      VARBINARY(112)
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE game.Characters
SET AutoHuntEnabled = @Enabled,
    AutoHuntConfig  = @Config,
    UpdatedAtUtc    = SYSUTCDATETIME()
WHERE CharacterId = @CharacterId;
END;
