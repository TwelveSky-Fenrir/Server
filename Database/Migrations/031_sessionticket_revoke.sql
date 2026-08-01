-- Lot 2 -- op23 FAIL_MOVE_ZONE doit revoquer le ticket de passation deja frappe.
--
-- LEGACY. Server/ts25login/S04_MyWork02.cpp:1613-1641 : quand le client avoue n'avoir pas atteint la zone,
-- le login remet mMoveZoneResult = FALSE puis emet U_LOGIN_REGISTER_USER_FOR_PLAYUSER_2_SEND. Ce n'est pas
-- cosmetique : register_2 FORCE mRegisterSort = LP_UPDATE_USER (Server/ts25playuser/S07_MyGame01.cpp:968),
-- or la zone ne peut prendre l'emplacement QUE depuis LP_MOVING_ZONE ou ZP_MOVING_ZONE
-- (Server/ts25playuser/S07_MyGame01.cpp:1057-1060). Le rollback rend donc la passation MECANIQUEMENT
-- impossible ensuite. L'equivalent Fenrir de "l'emplacement n'est plus en transit" est "le ticket de session
-- n'existe plus" -- et il manquait : usp_SessionTicket_Consume ne verifie que l'expiration, le ticket restait
-- consommable pendant tout son TTL (LoginServerOptions.TicketTtlSeconds = 15 s).
--
-- PORTEE DE LA REVOCATION. Meme cle que Create/Consume : AccountId. Le client legacy non modifie ne peut
-- presenter aucune preuve GUID/HMAC, seule son identite de compte -- l'existence d'un ticket valide POUR ce
-- compte est elle-meme la preuve (voir l'en-tete de runtime.SessionTickets). Revoquer par compte est donc
-- exactement aussi precis que consommer par compte, et un seul ticket actif existe par compte a la fois
-- (Create fait DELETE-puis-INSERT).
--
-- POURQUOI PAS REUTILISER usp_SessionTicket_Consume. Sa semantique est "entree monde effectuee" : il projette
-- le ticket vers l'appelant. La revocation est l'inverse (aucune sortie), et confondre les deux ferait du
-- LoginServer un consommateur de tickets -- exactement la confusion que l'en-tete de Consume met en garde
-- contre ("a blind retry here is the classic MMO ticket-dupe bug"). Deux verbes, deux procedures.
--
-- IDEMPOTENTE PAR CONSTRUCTION : un DELETE sans ligne correspondante est un no-op. Aucune sortie, donc aucun
-- oracle : un appelant ne peut pas distinguer "j'ai revoque" de "il n'y avait rien".
--
-- NATIVE_COMPILATION, SCHEMABINDING comme ses trois soeurs sur cette table memory-optimized SCHEMA_ONLY.
-- Consequence a connaitre pour une migration FUTURE : cette procedure schema-bind runtime.SessionTickets, elle
-- devra donc etre droppee et recreee par toute migration qui redrope la table -- exactement ce que
-- Migrations/001_sessiontickets_targetmapid.sql a du faire pour Create/Consume/Purge. Ce script s'applique
-- APRES 001 (ordre du manifeste), donc apres la recreation de la table portant TargetMapId.
--
-- PERMISSIONS inchangees : Schemas/002_roles.sql accorde EXECUTE au niveau du SCHEMA runtime, permission
-- couvrante heritee transitivement par tout objet cree dans ce schema, maintenant et plus tard.

CREATE PROCEDURE runtime.usp_SessionTicket_Revoke @AccountId INT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DELETE
    FROM runtime.SessionTickets
    WHERE AccountId = @AccountId;
END;
GO
