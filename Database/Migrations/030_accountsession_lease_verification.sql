-- Lot 2 -- le bail de session Login redevient un aller-retour VERIFIE.
--
-- LEGACY. Server/ts25login/S07_MyGame01.cpp:59-72 : toutes les 3 minutes, pour chaque session valide qui
-- n'est PAS en transit de zone, le login re-ancre son bail aupres de ts25playuser
-- (U_LOGIN_REGISTER_USER_FOR_PLAYUSER_2_SEND) et, si mRecv_Result != 0 -- typiquement parce que
-- l'emplacement a ete repris, libere ou fauche -- il FERME la session cliente. Le renouvellement n'est donc
-- pas un "poke" : c'est la seule preuve, cote login, qu'il possede encore le compte.
--
-- CE QUI MANQUAIT. runtime.usp_AccountSession_RefreshAndGetKicked ne peut pas porter cette preuve : son
-- UPDATE ... INNER JOIN @AccountIds touche silencieusement 0 ligne quand la ligne du compte a disparu, et son
-- SELECT filtre sur KickRequested = 1 -- vide par construction pour @ServerKind = 0 (voir l'en-tete de ce
-- fichier-la). Le LoginServer ne pouvait donc pas distinguer "bail renouvele" de "ligne evaporee".
-- Chemin d'echec concret : un second login du meme compte arrivant sur un AUTRE processus LoginServer prend
-- la branche ConflictLogin de usp_AccountSession_ClaimOrSignalKick, qui SUPPRIME la ligne
-- (usp_AccountSession_ClaimOrSignalKick.sql:69-73) ; la troisieme tentative en cree une NOUVELLE, avec un
-- SessionToken different. Le premier LoginServer continuait de servir sa session -> deux sessions Login
-- vivantes pour un meme compte.
--
-- POURQUOI LE SessionToken ET PAS SEULEMENT L'AccountId. Une verification par AccountId seul est reparee par
-- la fenetre de re-creation : entre la suppression et la 3e tentative gagnante il y a quelques secondes, alors
-- que le sondage de bail tourne toutes les LoginServerOptions.AccountSessionRefreshIntervalSeconds (60 s par
-- defaut). Un AccountId seul retrouverait la ligne du CONCURRENT et conclurait a tort que le bail tient.
-- La cle de propriete est donc (AccountId, SessionToken), exactement la meme que celle de
-- usp_AccountSession_ClearIfOwner -- qui refuse deja, pour la meme raison, de supprimer la ligne d'un autre.
--
-- POURQUOI UN NOUVEAU TYPE ET UNE NOUVELLE PROCEDURE plutot qu'une modification de l'existant.
-- Schemas/Types/runtime/tvp_AccountIdList.sql et
-- StoredProcedures/runtime/usp_AccountSession_RefreshAndGetKicked.sql sont deja listes dans _manifest.txt et
-- donc journalises SHA-256 : les editer ferait echouer la prochaine migration de toute base deja provisionnee.
-- Et surtout, changer la forme du jeu de resultats de RefreshAndGetKicked casserait son consommateur cote
-- GameServer (KickedAccountDto est mappe par ordinal). Les deux procedures coexistent : le Game garde la voie
-- "kick", le Login prend la voie "bail detenu".
--
-- INTERPRETEE, PAS NATIVE-COMPILEE, comme sa soeur RefreshAndGetKicked et pour la meme raison : les modules
-- natifs n'acceptent ni UPDATE ... FROM ni les parametres table. Les indications WITH (SNAPSHOT) rendent le
-- niveau d'isolation explicite quel que soit l'etat transactionnel ambiant de l'appelant.
--
-- PERMISSIONS. Schemas/002_roles.sql accorde EXECUTE au niveau du SCHEMA (GRANT EXECUTE ON SCHEMA::runtime),
-- permission couvrante heritee transitivement par tout objet cree dans ce schema, maintenant et plus tard --
-- y compris les types table. Ni le type ni la procedure ci-dessous n'ont besoin d'un GRANT propre.

-- 1. Le parametre table porte la cle de propriete complete, pas seulement l'identifiant de compte.
CREATE TYPE runtime.tvp_AccountSessionLease AS TABLE
(
    AccountId    INT              NOT NULL,
    SessionToken UNIQUEIDENTIFIER NOT NULL
);
GO

-- 2. Meme UPDATE de bail que RefreshAndGetKicked, mais restreint aux lignes dont l'appelant est PROUVE
--    proprietaire, puis renvoi de ces memes lignes. L'appelant deduit les baux perdus par difference : tout
--    compte envoye et non renvoye n'appartient plus a ce processus, et sa session locale doit etre fermee.
--    SessionState = 1 (TearingDown) n'est PAS exclu : cet etat est pose par le processus proprietaire lui-meme
--    pendant sa propre fermeture, ce n'est pas une perte de bail.
CREATE PROCEDURE runtime.usp_AccountSession_RefreshAndGetHeldLeases @ServerKind TINYINT,
                                                                    @ShardId TINYINT NULL,
                                                                    @Leases runtime.tvp_AccountSessionLease READONLY
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE s
    SET s.LastRefreshedUtc = SYSUTCDATETIME()
    FROM runtime.AccountSessions AS s
             WITH (SNAPSHOT)
             INNER JOIN @Leases AS l
                        ON l.AccountId = s.AccountId
                            AND l.SessionToken = s.SessionToken
    WHERE s.ServerKind = @ServerKind
      AND (@ShardId IS NULL
        OR s.ShardId = @ShardId);

    SELECT s.AccountId
    FROM runtime.AccountSessions AS s WITH (SNAPSHOT)
             INNER JOIN @Leases AS l
                        ON l.AccountId = s.AccountId
                            AND l.SessionToken = s.SessionToken
    WHERE s.ServerKind = @ServerKind
      AND (@ShardId IS NULL
        OR s.ShardId = @ShardId);
END;
GO
