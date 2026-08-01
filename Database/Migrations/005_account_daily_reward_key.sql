-- La recompense quotidienne repasse au niveau COMPTE, comme le legacy.
--
-- CE QUE FAIT LE LEGACY (verifie ligne a ligne)
-- Les deux champs vivent sur la table de COMPTE `memberinfo`, pas sur `avatarinfo` :
--   `uRewardClaimDay` int(11) DEFAULT 0 / `uRewardClaimState` smallint(6) DEFAULT 0
--   Server/BuildEU33/DB/nxtserver.sql:1153-1154 (index unique q01 sur uUserIdx, :1156).
-- La persistance est keyee sur l'index de compte, sans le moindre uCharIdx :
--   UPDATE %s SET uRewardClaimDay=%d,uRewardClaimState=%d WHERE uUserIdx=%d
--   Server/ts25playuser/S08_MyDB.cpp:400 (appelee Server/ts25playuser/S07_MyGame01.cpp:740).
-- En memoire le porteur est PLAY_INFO (Server/Header/Protocol/STRUCT.h:1467-1468), alloue UNE FOIS PAR
-- COMPTE CONNECTE : cree a la connexion du compte (Server/ts25playuser/S07_MyGame01.cpp:924-942, qui pose
-- z->uRewardClaimDay/State depuis la ligne memberinfo relue par le login, S08_MyDB.cpp:247-248), libere a la
-- deconnexion du compte (:1013-1029). Le handler lit `tUserInfo->mPlayInfo->uRewardClaimState`
-- (Server/ts25zone/S04_MyWork02.cpp:15327) et l'ecrit a TRUE (:15366) sur ce meme enregistrement de compte.
--
-- LE POINT DECISIF : la selection de personnage NE REINITIALISE PAS le compteur.
-- RegisterUserForLogin_03 (Server/ts25playuser/S07_MyGame01.cpp:971-1011) reecrit uCharIdx, uCharDate,
-- uAvatarInfo et uStateInfo -- et ne touche NI uRewardClaimDay NI uRewardClaimState. Le meme enregistrement,
-- donc le meme drapeau, est reutilise tel quel par le personnage suivant du meme compte. C'est tout le
-- mecanisme anti-double-reclamation du legacy : il n'y a ni verrou ni contrainte SQL, seulement le fait que
-- le drapeau est PORTE PAR LE COMPTE.
--
-- L'ECART FENRIR
-- game.Characters.RewardClaimDay/RewardClaimDate (Tables/game/Characters.sql:121-125) sont keyes
-- CharacterId, et les trois procedures en vigueur le sont aussi. Le plafond de personnages est le meme des
-- deux cotes -- MAX_USER_AVATAR_NUM = 3 (Server/Header/Protocol/DEFINE.h:277), uCharIdx0/1/2
-- (Server/BuildEU33/DB/nxtserver.sql:1097-1099), CK_Characters_Slot CHECK (Slot BETWEEN 0 AND 2)
-- (Tables/game/Characters.sql:204) -- donc un compte Fenrir peut reclamer la recompense du jour jusqu'a
-- 3 fois la ou le legacy n'en autorise qu'une. Facteur exact : 3.
--
-- POURQUOI UNE TABLE game.AccountDailyRewards ET NON DEUX COLONNES SUR auth.Accounts
-- auth.Accounts est l'equivalent direct de `memberinfo`, mais y poser deux colonnes ecrites par le
-- GameServer traverserait une frontiere que ce depot tient explicitement : Schemas/002_roles.sql documente
-- fenrir_game_role comme "owns character data and world-entry flow, plus runtime; never auth", et ne lui
-- accorde AUCUN droit sur le schema auth. Une procedure game.* ecrivant auth.Accounts ne fonctionnerait que
-- par chainage de proprietaire -- un contournement invisible de ce posture, qui casserait le jour ou les
-- schemas cessent de partager un proprietaire. La table vit donc dans game, couverte telle quelle par le
-- GRANT EXECUTE ON SCHEMA::game existant : aucun nouveau grant n'est necessaire. La FK vers auth.Accounts
-- reste licite (une FK n'est pas une permission) et suit la convention de nommage cross-schema
-- FK_<ChildTable>_<TargetSchema>_<Role> documentee par Tables/game/Characters.sql:206-207.
--
-- POURQUOI UN NOUVEAU SCRIPT PLUTOT QU'UNE EDITION
-- Tables/game/Characters.sql et les trois procedures usp_Character_*RewardClaim* sont deja listes dans
-- _manifest.txt (:485, :486, :554), donc journalises par SHA-256 : Fenrir.Tools.DbMigrator refuse de
-- re-appliquer un chemin journalise dont le contenu a change. Une base FRAICHE applique base-puis-migration,
-- une base PERSISTANTE saute les scripts deja journalises et n'applique que celle-ci : les deux convergent.
--
-- CE QUE CE SCRIPT NE FAIT PAS, DELIBEREMENT
-- Il ne SUPPRIME PAS game.Characters.RewardClaimDay/RewardClaimDate. Ces deux colonnes deviennent
-- non-autoritaires et figees (plus aucun ecrivain apres cette migration : le repository C# est repointe dans
-- le meme lot), et sont conservees en l'etat comme trace exacte de l'etat par personnage d'avant la bascule.
-- C'est la garantie "aucune perte silencieuse" : la fusion ci-dessous est integralement reconstituable depuis
-- la source, et un DROP COLUMN serait irreversible sans sauvegarde. Leur suppression est un script ulterieur,
-- a ecrire une fois la bascule verifiee en exploitation.
-- Il ne touche pas non plus a usp_Character_ResetDailyRewardClaims / _ClaimDailyReward / _GetRewardClaimState :
-- elles restent en base, simplement plus jamais appelees.

IF OBJECT_ID('game.AccountDailyRewards', 'U') IS NULL
CREATE TABLE game.AccountDailyRewards
(
    AccountId       INT          NOT NULL,
    RewardClaimDay  TINYINT      NOT NULL
        CONSTRAINT DF_AccountDailyRewards_RewardClaimDay DEFAULT 0
        CONSTRAINT CK_AccountDailyRewards_RewardClaimDay CHECK (RewardClaimDay BETWEEN 0 AND 7),
    RewardClaimDate INT          NOT NULL
        CONSTRAINT DF_AccountDailyRewards_RewardClaimDate DEFAULT 0,
    UpdatedAtUtc    DATETIME2(3) NOT NULL
        CONSTRAINT DF_AccountDailyRewards_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AccountDailyRewards PRIMARY KEY CLUSTERED (AccountId),
    CONSTRAINT FK_AccountDailyRewards_Auth_Account FOREIGN KEY (AccountId) REFERENCES auth.Accounts (AccountId)
);
GO

-- BACKFILL -- REGLE DE CONFLIT, EXPLICITE ET CONSERVATRICE
--
-- Un compte peut avoir jusqu'a 3 lignes game.Characters portant chacune un etat de reclamation. Les fondre
-- en une seule ligne de compte demande de trancher, et le sens du choix n'est pas symetrique :
--   * trop PERMISSIF -> le compte reclame une seconde fois aujourd'hui : duplication d'objet, degat
--     economique reel et non borne (l'objet reste en jeu indefiniment) ;
--   * trop RESTRICTIF -> le compte perd au pire UNE journee de recompense, pour UN compte, et l'etat
--     s'auto-repare au prochain reset quotidien.
-- La regle retenue est donc le MAXIMUM, colonne par colonne :
--   * RewardClaimDate = MAX(...) : si UN SEUL personnage a deja reclame aujourd'hui, le compte entier compte
--     comme ayant reclame aujourd'hui.
--   * RewardClaimDay  = MAX(...) : le compte herite du compteur hebdomadaire LE PLUS AVANCE, donc du plus
--     petit nombre de reclamations restantes de la semaine.
-- Les deux MAX sont pris INDEPENDAMMENT, et peuvent donc provenir de deux personnages differents : la ligne
-- produite est l'etat le plus restrictif ATTEIGNABLE, pas l'etat d'un personnage particulier. C'est
-- volontaire, et c'est le seul choix qui ne peut pas offrir une reclamation supplementaire a personne.
--
-- La ligne source de chaque valeur reste lisible dans game.Characters, non modifiee (cf. en-tete).
--
-- HAVING : les comptes dont aucun personnage n'a jamais rien reclame n'ont pas de ligne du tout. L'absence de
-- ligne EST l'etat (0, 0) -- les procedures ci-dessous le traitent explicitement. Sur une base fraiche
-- game.Characters est vide et ce INSERT ne produit rien.
-- NOT EXISTS : rejeu sans effet si le script est relance sur une base deja migree.
INSERT INTO game.AccountDailyRewards (AccountId, RewardClaimDay, RewardClaimDate, UpdatedAtUtc)
SELECT c.AccountId,
       MAX(c.RewardClaimDay),
       MAX(c.RewardClaimDate),
       SYSUTCDATETIME()
FROM game.Characters c
WHERE NOT EXISTS (SELECT 1
                  FROM game.AccountDailyRewards a
                  WHERE a.AccountId = c.AccountId)
GROUP BY c.AccountId
HAVING MAX(c.RewardClaimDay) <> 0
    OR MAX(c.RewardClaimDate) <> 0;
GO

-- Meme calcul de reset hebdomadaire paresseux que usp_Character_GetRewardClaimState, transpose au compte :
-- DATEDIFF(DAY,0,x)/7 et non DATEDIFF(WEEK,0,x), qui suivrait le DATEFIRST de la session au lieu du lundi
-- reel du jour zero. Le legacy, lui, ecrit ce reset depuis le tick du Center le lundi
-- (Server/ts25center/S07_MyGame01.cpp:228-231, sous `if (DateTime::WDay() == 1)`).
--
-- Renvoie EXACTEMENT une ligne pour un compte connu, AUCUNE pour un compte inconnu : l'appelant C# traite
-- deja null comme "compte/personnage inconnu" et coupe la session. L'existence est verifiee contre
-- game.Characters et non auth.Accounts -- meme raison que l'en-tete : game ne lit pas auth. Un compte sans
-- aucun personnage ne peut de toute facon pas etre en zone pour envoyer l'opcode 154/155.
CREATE OR ALTER PROCEDURE game.usp_AccountDailyReward_GetState @AccountId INT, @TodayDate INT
AS
BEGIN
    SET
        NOCOUNT ON;

    IF
        NOT EXISTS (SELECT 1
                    FROM game.Characters
                    WHERE AccountId = @AccountId)
        RETURN;

    DECLARE
        @TodayAsDate DATE = TRY_CONVERT(DATE, CAST(@TodayDate AS VARCHAR(8)), 112);
    DECLARE
        @Day TINYINT = 0;
    DECLARE
        @Date INT = 0;

    SELECT @Day = RewardClaimDay,
           @Date = RewardClaimDate
    FROM game.AccountDailyRewards
    WHERE AccountId = @AccountId;

    -- CAST en TINYINT : melanger une branche TINYINT et un litteral INT promeut en INT, que le champ
    -- TINYINT de l'appelant rejette.
    SELECT RewardClaimDay  = CAST(CASE
                                      WHEN @Date <> 0
                                          AND DATEDIFF(DAY, 0, @TodayAsDate) / 7 <>
                                              DATEDIFF(DAY, 0, TRY_CONVERT(DATE, CAST(@Date AS VARCHAR(8)), 112)) / 7
                                          THEN 0
                                      ELSE @Day
        END AS TINYINT),
           RewardClaimDate = @Date;
END;
GO

-- L'etat de reclamation est keye COMPTE, mais l'objet atterrit dans l'inventaire du PERSONNAGE qui reclame :
-- la procedure prend donc les deux cles. @CharacterId est valide comme appartenant a @AccountId avant toute
-- ecriture -- les deux viennent de la meme session authentifiee, mais l'invariant est bon marche a tenir ici
-- et rend impossible d'ecrire l'inventaire d'un personnage d'un autre compte.
--
-- La ligne de compte est creee a la demande (upsert) : le backfill ci-dessus n'a insere que les comptes
-- ayant deja reclame, et un compte cree apres cette migration n'a pas de ligne. UPDLOCK+HOLDLOCK sur le test
-- d'existence prend un verrou de plage sur la cle absente, ce qui serialise deux INSERT concurrents pour le
-- meme compte au lieu de produire une violation de PK.
--
-- L'invariant reel reste le WHERE de l'UPDATE, pas la lecture de @IsNewWeek : c'est lui qui rend la double
-- reclamation impossible, y compris entre deux personnages du meme compte connectes simultanement sur deux
-- shards -- cas que le legacy, lui, ne pouvait pas produire (un seul PLAY_INFO par compte).
CREATE OR ALTER PROCEDURE game.usp_AccountDailyReward_Claim @AccountId INT,
                                                            @CharacterId INT,
                                                            @TodayDate INT,
                                                            @Container TINYINT,
                                                            @Items game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DECLARE
        @TodayAsDate DATE = TRY_CONVERT(DATE, CAST(@TodayDate AS VARCHAR(8)), 112);

    -- Hors transaction : l'appartenance d'un personnage a un compte ne change jamais, et echouer ici ne doit
    -- pas ouvrir puis annuler une transaction pour rien.
    IF
        NOT EXISTS (SELECT 1
                    FROM game.Characters
                    WHERE CharacterId = @CharacterId
                      AND AccountId = @AccountId)
        THROW 50359, N'Daily reward: character does not belong to the claiming account.', 1;

    BEGIN
        TRANSACTION;

    IF
        NOT EXISTS (SELECT 1
                    FROM game.AccountDailyRewards
                    WITH (UPDLOCK, HOLDLOCK)
                    WHERE AccountId = @AccountId)
        INSERT INTO game.AccountDailyRewards (AccountId)
        VALUES (@AccountId);

    DECLARE
        @IsNewWeek BIT;
    SELECT @IsNewWeek = CASE
                            WHEN RewardClaimDate = 0 THEN 0
                            WHEN DATEDIFF(DAY, 0, @TodayAsDate) / 7 <>
                                 DATEDIFF(DAY, 0, TRY_CONVERT(DATE, CAST(RewardClaimDate AS VARCHAR(8)), 112)) / 7
                                THEN 1
                            ELSE 0
        END
    FROM game.AccountDailyRewards
    WHERE AccountId = @AccountId;

    UPDATE game.AccountDailyRewards
    SET RewardClaimDay  = IIF(@IsNewWeek = 1, 1, RewardClaimDay + 1),
        RewardClaimDate = @TodayDate,
        UpdatedAtUtc    = SYSUTCDATETIME()
    WHERE AccountId = @AccountId
      AND RewardClaimDate <> @TodayDate
      AND (RewardClaimDay BETWEEN 0 AND 6 OR @IsNewWeek = 1);

    IF
        @@ROWCOUNT = 0
        THROW 50270, N'Daily reward already claimed today, fully claimed, or unknown account.', 1;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
      AND Container = @Container;

    INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                     Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
    SELECT @CharacterId,
           @Container,
           Slot,
           ItemId,
           Quantity,
           Enchant,
           Combine,
           Refine,
           Socket,
           SocketGem1,
           SocketGem2,
           SocketGem3,
           ExpireDate,
           Serial
    FROM @Items;

    COMMIT TRANSACTION;
END;
GO

-- Equivalent au niveau compte de usp_Character_ResetDailyRewardClaims, et desormais le seul appele.
-- Legacy : `UPDATE MemberInfo SET uRewardClaimState=0;` tous les jours, sans WHERE
-- (Server/ts25center/S07_MyGame01.cpp:233), et `UPDATE MemberInfo SET uRewardClaimDay=0;` uniquement le lundi
-- (:228-231, MONDAY = 1 dans l'enum WDAY, Server/ts25zone/H07_MyGame.h:40-48). RewardClaimDate = 0 est le
-- marqueur "non reclame" cote Fenrir, donc l'equivalent exact de uRewardClaimState = 0.
-- Un UPDATE unique et garde, donc pas de transaction explicite (une instruction est deja atomique) ; le WHERE
-- evite de reecrire les lignes deja a zero. Ne toucher aucun compte n'est pas une erreur : pas de THROW.
CREATE OR ALTER PROCEDURE game.usp_AccountDailyReward_ResetAll @ClearWeeklyDayCounter BIT
AS
BEGIN
    SET
        NOCOUNT ON;

    UPDATE game.AccountDailyRewards
    SET RewardClaimDate = 0,
        RewardClaimDay  = CASE WHEN @ClearWeeklyDayCounter = 1 THEN 0 ELSE RewardClaimDay END,
        UpdatedAtUtc    = SYSUTCDATETIME()
    WHERE RewardClaimDate <> 0
       OR (@ClearWeeklyDayCounter = 1 AND RewardClaimDay <> 0);
END;
GO
