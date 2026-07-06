-- Corrective fix for game.usp_TribeRole_GetForCharacter (never edited in place -- DbMigrator journals every
-- script by content hash and refuses a changed re-apply).
--
-- Bug: the CASE expression's branches (1, 2, 0) are untyped integer literals, which T-SQL infers as INT --
-- there is no CAST anywhere in the original proc. Fenrir.Data.Tribes.TribeRepository.GetRoleForCharacterAsync
-- reads it via CaeriusNet's Db.ExecuteScalarAsync<byte>(sp, ct), which unboxes ADO.NET's returned
-- System.Int32 value directly to byte -- an operation that throws System.InvalidCastException
-- ("Unable to cast object of type 'System.Int32' to type 'System.Byte'") regardless of the actual numeric
-- value, since C# unboxing requires an exact runtime-type match, not just a widening-compatible value.
--
-- This call happens on every EnterWorldService.HandleAsync (every world entry, for every character, tribe or
-- not) -- so every single successful zone handshake/EnterWorldRequest was ending in this exception, caught by
-- the try/catch added earlier this session (Application/Fenrir.Application.Game.Services/ZoneLifecycle/
-- EnterWorldService.cs's CompleteWorldEntryAsync wrapper), which logs at LogError and aborts the session --
-- exactly matching the previously-unexplained "client stuck at loading 100%, then idle-timeout disconnect"
-- symptom: the client's EnterWorldRequest genuinely was received and processed, but the session was aborted
-- immediately afterward before either of the two ZC_ZPACKET_OK_SEND-compressed response payloads or the
-- self-spawn packet could be observed as a clean success by the client.
--
-- Fix: CAST the CASE expression to TINYINT, matching the byte-sized 0/1/2 role encoding
-- game.usp_TribeRole_GetForCharacter's own header comment already documents (Server/Header/function.h:92-114's
-- ReturnTribeRole) and matching the C# repository method's own declared return type.
CREATE OR ALTER PROCEDURE game.usp_TribeRole_GetForCharacter @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TribeRole =
           CAST(
               CASE
                   WHEN master.TribeId IS NOT NULL THEN 1
                   WHEN sub.CharacterId IS NOT NULL THEN 2
                   ELSE 0
                   END AS TINYINT)
    FROM game.Characters AS c
             LEFT JOIN game.Tribes AS master
                       ON master.TribeId = c.Tribe AND master.MasterCharacterId = c.CharacterId
             LEFT JOIN game.TribeSubMasters AS sub
                       ON sub.TribeId = c.Tribe AND sub.CharacterId = c.CharacterId
    WHERE c.CharacterId = @CharacterId;
END;
GO
