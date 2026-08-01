-- Lot 4 -- auto-buff (liste enregistree + date de validite) et rank-buff (palier + sa date de remise).
-- Trois champs mutes en jeu cote Fenrir, persistes par le legacy, et perdus a chaque deconnexion.
--
-- CE QUE LE LEGACY PERSISTE, ET OU
--   * aAutoBuffSkill  texte    Server/Header/CSQLAvatar.cpp:703 (FIELD_AVATAR1), schema
--                              Server/BuildEU33/DB/nxtserver.sql:134 (varchar(48)), porteur
--                              Server/Header/Protocol/STRUCT.h:451 (aAutoBuffSkill[8][2]).
--   * aAutoBuffTime   entier   Server/Header/CSQLAvatar.cpp:672, schema nxtserver.sql:133, porteur STRUCT.h:450.
--   * aRankPointDate  entier   Server/Header/CSQLAvatar.cpp:646, schema nxtserver.sql:151, porteur STRUCT.h:490.
--   * aRankBuffType   entier   Server/Header/CSQLAvatar.cpp:647, schema nxtserver.sql:152, porteur STRUCT.h:491.
-- Les trois FIELD_AVATAR0/1 sont hors de tout #ifdef : ils comptent dans ReleaseM33 comme dans ReleaseEU33.
--
-- game.Characters.AutoBuffTime existe deja (colonne de base) mais n'est ecrite QUE par
-- usp_Character_CreateWithStarterKit, et bien que projetee par usp_Character_GetForWorldEntry elle n'etait
-- jamais transmise a PlayerRuntimeState. Ce script n'ajoute donc que TROIS colonnes, puis cable les QUATRE
-- champs dans les deux sens -- une colonne persistee mais jamais rechargee est pire qu'une colonne absente :
-- elle donne l'illusion de fonctionner.
--
-- CE QUE CE SCRIPT NE FAIT PAS : DrunkBottleIndex
-- Le quatrieme champ du lot (aBottleIndex, Server/Header/CSQLAvatar.cpp:677) est livre par
-- Migrations/008_avatar_counters_and_bottles_writeback.sql, qui persiste en plus BottleSlots -- l'index seul
-- ne veut rien dire sans les emplacements qu'il designe. Le dupliquer ici ferait echouer durement l'ALTER
-- (colonne deja presente) et creerait deux ecritures concurrentes sur la meme donnee.
--
-- ENCODAGE DE AutoBuffSkill : LE FORMAT LEGACY, VERBATIM
-- Le legacy serialise les 8 emplacements x 2 valeurs en un tampon texte a largeur fixe : la boucle
-- Server/Header/CSQLAvatar.cpp:421-424 appelle CREATE_INT(..., pad = 3, EFIX_NONE) deux fois par emplacement,
-- et CreateIntPad (Server/Header/CSQLDatabase.cpp:511-528) ecrit "%03d" de (valeur % 1000). Soit 8 x 2 x 3 = 48
-- caracteres -- MAX_AUTO_BUFF_TEXT_LENGTH = ((3+3)*8)+1 = 49 avec le NUL (Server/Header/CSQLDatabase.h:62,111)
-- --, ordre = numero de competence puis grade, emplacement par emplacement, dans l'ordre du tableau
-- (MAX_AUTO_BUFF_SKILL_NUM = 8 et MAX_AUTO_BUFF_VALUE_NUM = 2, Server/Header/Protocol/DEFINE.h:346-347).
-- EFIX_NONE ramene une valeur negative a 0 (CSQLAvatar.cpp:19-23) et le tampon est pre-rempli de '0' avant
-- chaque INSERT/UPDATE (macro TYPE(FILL), CSQLAvatar.cpp:278, 301-303) : un emplacement vide se sauvegarde en
-- '000000', jamais en chaine vide. La relecture est symetrique -- CreateIntVal (CSQLDatabase.cpp:530-544)
-- atoi() de la tranche de 3 caracteres. NVARCHAR(48) NOT NULL reproduit ce contrat a l'identique : la colonne
-- reste lisible et ecrivable par les deux implementations. Les identifiants de world.Skills tiennent tous sur
-- 3 chiffres, l'encodage est donc sans perte.
--
-- AutoBuffTime : UNE DATE, PAS UN COMPTEUR
-- Malgre son suffixe, aAutoBuffTime est une date d'expiration YYYYMMDD, coherente avec son alias legacy
-- aContinueSkillDay (STRUCT.h:450) : la prolongation passe par ReturnAddDate
-- (Server/ts25zone/S04_MyWork03.cpp:3150-3156) et l'activation compare a ReturnNowDate
-- (Server/ts25zone/S04_MyWork02.cpp:12705, meme comparaison au tick Server/ts25zone/S07_MyGame04.cpp:2287).
-- Jamais decremente par tick. La colonne existe deja avec cette semantique ; seul son cablage manquait.
--
-- RankBuffType : REMISE QUOTIDIENNE, DONC INDISSOCIABLE DE RankPointDate
-- MyUtil::ResetRank (Server/ts25zone/S07_MyGame03.cpp:8283-8385) remet aRankPoint ET aRankBuffType a zero des
-- que aRankPointDate n'est plus la date du jour, et amorce aRankPointDate au premier passage (:8286-8290).
-- Persister le palier sans sa date le rendrait eternel : les deux colonnes vont ensemble, comme elles sont
-- contigues dans le schema legacy (nxtserver.sql:151-152) et dans la liste de sauvegarde (CSQLAvatar.cpp:645-647).
-- aRankPoint n'a AUCUN site d'incrementation cote Fenrir aujourd'hui (uniquement des remises a zero, dans
-- Zone.HolyStoneBattleReset) : aucune colonne n'est creee pour lui, ce serait une constante 0.
-- Borne 0..7 : 0 = aucun buff, 1..7 = les sept paliers consommes par MyFactor
-- (Server/Header/Protocol/MyFactor.cpp:1845, 3469, 3543, 3615, 3657, 3717, 3761). La borne est sure cote
-- appelant : RankBuffResolver rejette tout sort hors 1..7 avant que la commande n'atteigne la zone. La branche
-- de validation amont qui compile est bien le #else (comptage de steles, S04_MyWork02.cpp:13743 et suivantes),
-- USE_RANK_POINT etant commente (Server/Header/Protocol/DEFINE.h:75) ; l'affectation elle-meme est apres le
-- #endif (S04_MyWork02.cpp:13781), donc vivante dans les deux variantes.
--
-- POURQUOI UN NOUVEAU SCRIPT PLUTOT QU'UNE EDITION DES FICHIERS DE BASE
-- Tables/game/Characters.sql, Schemas/Types/game/tvp_CharacterProgress.sql,
-- StoredProcedures/game/usp_Character_PersistProgressBatch.sql, usp_Character_PersistFinalFlush.sql et
-- usp_Character_GetForWorldEntry.sql sont deja listes dans _manifest.txt, donc journalises par SHA-256 par
-- Fenrir.Tools.DbMigrator sur toute base qui les a appliques une fois (le volume de dev AppHost est
-- persistant). Le migrateur refuse de re-appliquer un chemin journalise dont le contenu a change : une edition
-- en place ferait echouer durement la prochaine execution. Une base FRAICHE applique les scripts de base puis
-- les migrations ; une base PERSISTANTE saute les scripts deja journalises. Les deux convergent.
--
-- AVERTISSEMENT : QUATRE AUTRES LOTS ETENDENT LE MEME TVP EN PARALLELE
-- game.tvp_CharacterProgress est un type TABLE : il ne peut pas etre ALTERe, chaque lot le DROP et le recree
-- EN ENTIER, et le dernier applique fixe la forme finale. Au moment ou ce script est ecrit, cinq lots se
-- partagent ce type (etat des drapeaux d'avatar, cosmetiques et soldes, penderie et compagnon, auto-chasse et
-- premium, compteurs et bouteilles, celui-ci) et l'enregistrement C# CharacterProgressTvp est deja en avance
-- sur la derniere migration ENREGISTREE. Ce script s'ancre sur la derniere migration reellement enregistree au
-- manifeste (007) : il s'applique proprement seul, sans dependre d'un script non enregistre. LE LOT QUI
-- S'ENREGISTRERA EN DERNIER DOIT DECLARER L'UNION de toutes les colonnes, dont les quatre d'ici --
-- AutoBuffTime / AutoBuffSkill / RankPointDate / RankBuffType -- en queue du type, des deux procedures de
-- write-behind et de la projection RS0. En oublier une la retire silencieusement du chemin d'ecriture.
--
-- OU CE SCRIPT SE POSE DANS LA CHAINE
-- L'ordre d'application est l'ordre du MANIFESTE, pas l'ordre alphabetique des fichiers
-- (Fenrir.Tools.DbMigrator/MigrationRunner.cs:35 itere la liste rendue par ManifestReader). Ce script reprend
-- la forme du type a 37 colonnes livree par Migrations/007_costume_and_companion_timer_writeback.sql
-- (elle-meme construite sur les 33 de Migrations/006_avatar_state_flags_writeback.sql, elles-memes sur les 30
-- de Migrations/002) et y ajoute les quatre siennes, pour 41. Son entree du manifeste est donc placee apres
-- celle de 007. Toute migration ulterieure qui recree game.tvp_CharacterProgress doit repartir de CES 41
-- colonnes : un type TABLE ne peut pas etre ALTERe, chaque lot le reconstruit en entier, et le dernier
-- applique fixe la forme finale -- en oublier une la supprime silencieusement du chemin d'ecriture.
--
-- POURQUOI ALTER ADD POUR LES COLONNES ET DROP+CREATE POUR LE TYPE
-- game.Characters porte de vraies donnees joueur : un DROP+CREATE les detruirait. Un ALTER additif avec DEFAULT
-- sur une colonne NOT NULL est une operation de metadonnees seule sur SQL Server (Microsoft Learn, "Add Columns
-- to a Table") -- aucune reecriture de pages. La table n'est ni schema-bound, ni referencee par une vue
-- indexee, ni porteuse d'un index columnstore. A l'inverse un type TABLE ne peut ni etre ALTERe pour gagner des
-- colonnes ni etre supprime tant qu'une procedure le prend en parametre : les deux procedures de write-behind
-- sont donc supprimees, le type recree, puis les procedures recreees -- la sequence de Migrations/002.
-- usp_Character_GetForWorldEntry ne prend aucun TVP : CREATE OR ALTER suffit.
--
-- LES PERMISSIONS NE BOUGENT PAS : Schemas/002_roles.sql accorde EXECUTE au niveau du SCHEMA
-- (GRANT EXECUTE ON SCHEMA::game TO fenrir_game_role), permission couvrante heritee par tout objet du schema,
-- present et futur. Aucune des trois procedures ne porte de grant au niveau objet.

-- 1. Les trois colonnes manquantes. Garde d'idempotence sur la premiere : elles sont ajoutees dans le meme
--    ALTER, donc soit toutes presentes soit toutes absentes. Valeurs par defaut sures pour les lignes
--    existantes -- 48 zeros = les 8 emplacements vides dans l'encodage legacy, 0 = pas de rank-buff et pas de
--    date amorcee (ResetRank amorcera la date au premier passage, exactement comme pour un avatar legacy neuf).
IF COL_LENGTH('game.Characters', 'AutoBuffSkill') IS NULL
ALTER TABLE game.Characters
    ADD AutoBuffSkill NVARCHAR(48) NOT NULL
            CONSTRAINT DF_Characters_AutoBuffSkill DEFAULT
                N'000000000000000000000000000000000000000000000000',
        RankPointDate INT NOT NULL
            CONSTRAINT DF_Characters_RankPointDate DEFAULT 0,
        RankBuffType INT NOT NULL
            CONSTRAINT DF_Characters_RankBuffType DEFAULT 0
            CONSTRAINT CK_Characters_RankBuffType CHECK (RankBuffType BETWEEN 0 AND 7);
GO

-- 2. Les deux procedures qui referencent le type bloquent son DROP : les supprimer d'abord, puis le type.
DROP PROCEDURE IF EXISTS game.usp_Character_PersistFinalFlush;
DROP PROCEDURE IF EXISTS game.usp_Character_PersistProgressBatch;
DROP TYPE IF EXISTS game.tvp_CharacterProgress;
GO

-- 3. Le type, recree avec les quatre colonnes de fin. Corps identique a la forme a 37 colonnes livree par
--    Migrations/007_costume_and_companion_timer_writeback.sql, plus les quatre ajoutees. Meme classe que
--    DropItemTime / Eat*Potion / Mount* / VisibleState : de l'etat mono-proprietaire de PlayerRuntimeState,
--    re-encode a chaque flush, jamais un solde -- Money/BigMoney restent absents, ils ne bougent que par
--    usp_Character_AdjustMoney pour qu'un flush last-write-wins ne puisse jamais ecraser un solde.
--    AutoBuffSkill porte le NVARCHAR(48) du format legacy ; le mapper TVP genere par CaeriusNet emet
--    NVarChar(Max) cote client et SQL Server convertit vers la colonne etroite, comme deja pour SocketData
--    (Schemas/Types/game/tvp_AccountVaultItemSlot.sql:10, NVARCHAR(50)).
CREATE TYPE game.tvp_CharacterProgress AS TABLE
(
    CharacterId        INT          NOT NULL,
    FlushSequence      BIGINT       NOT NULL,
    Level              SMALLINT     NOT NULL,
    Level2             SMALLINT     NOT NULL,
    Experience         BIGINT       NOT NULL,
    Life               INT          NOT NULL,
    MaxLife            INT          NOT NULL,
    Mana               INT          NOT NULL,
    MaxMana            INT          NOT NULL,
    StatVit            INT          NOT NULL,
    StatStr            INT          NOT NULL,
    StatInt            INT          NOT NULL,
    StatDex            INT          NOT NULL,
    StatPoints         INT          NOT NULL,
    SkillPoints        INT          NOT NULL,
    ContributionPoints INT          NOT NULL,
    Exp2               INT          NOT NULL,
    RebirthCount       INT          NOT NULL,
    EatLifePotion      INT          NOT NULL,
    EatManaPotion      INT          NOT NULL,
    EatStrPotion       INT          NOT NULL,
    EatDexPotion       INT          NOT NULL,
    EatElePotion       INT          NOT NULL,
    DropItemTime       INT          NOT NULL,
    M15PetLuckyBoxPity INT          NOT NULL,
    MountItemId        INT          NOT NULL,
    MountExpActivity   INT          NOT NULL,
    MountPower         INT          NOT NULL,
    MountSlotIndex     INT          NOT NULL,
    MountTime          INT          NOT NULL,
    VisibleState       INT          NOT NULL,
    SpecialState       INT          NOT NULL,
    UseOrnament        INT          NOT NULL,
    PetExpX2Time       INT          NOT NULL,
    AnimalAbsorbTime   INT          NOT NULL,
    AnimalAbsorbState  INT          NOT NULL,
    CostumeIndex       INT          NOT NULL,
    AutoBuffTime       INT          NOT NULL,
    AutoBuffSkill      NVARCHAR(48) NOT NULL,
    RankPointDate      INT          NOT NULL,
    RankBuffType       INT          NOT NULL
);
GO

-- 4. Le flush periodique, recree avec les quatre SET supplementaires. Garde d'idempotence (FlushSequence
--    strictement croissante, une seule sequence monotone par personnage partagee par les deux saveurs de
--    flush) inchangee.
CREATE PROCEDURE game.usp_Character_PersistProgressBatch @Progress game.tvp_CharacterProgress READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE c
    SET c.Level              = s.Level,
        c.Level2             = s.Level2,
        c.Experience         = s.Experience,
        c.Life               = s.Life,
        c.MaxLife            = s.MaxLife,
        c.Mana               = s.Mana,
        c.MaxMana            = s.MaxMana,
        c.StatVit            = s.StatVit,
        c.StatStr            = s.StatStr,
        c.StatInt            = s.StatInt,
        c.StatDex            = s.StatDex,
        c.StatPoints         = s.StatPoints,
        c.SkillPoints        = s.SkillPoints,
        c.ContributionPoints = s.ContributionPoints,
        c.Exp2               = s.Exp2,
        c.RebirthCount       = s.RebirthCount,
        c.EatLifePotion      = s.EatLifePotion,
        c.EatManaPotion      = s.EatManaPotion,
        c.EatStrPotion       = s.EatStrPotion,
        c.EatDexPotion       = s.EatDexPotion,
        c.EatElePotion       = s.EatElePotion,
        c.DropItemTime       = s.DropItemTime,
        c.M15PetLuckyBoxPity = s.M15PetLuckyBoxPity,
        c.MountItemId        = s.MountItemId,
        c.MountExpActivity   = s.MountExpActivity,
        c.MountPower         = s.MountPower,
        c.MountSlotIndex     = s.MountSlotIndex,
        c.MountTime          = s.MountTime,
        c.VisibleState       = s.VisibleState,
        c.SpecialState       = s.SpecialState,
        c.UseOrnament        = s.UseOrnament,
        c.PetExpX2Time       = s.PetExpX2Time,
        c.AnimalAbsorbTime   = s.AnimalAbsorbTime,
        c.AnimalAbsorbState  = s.AnimalAbsorbState,
        c.CostumeIndex       = s.CostumeIndex,
        c.AutoBuffTime       = s.AutoBuffTime,
        c.AutoBuffSkill      = s.AutoBuffSkill,
        c.RankPointDate      = s.RankPointDate,
        c.RankBuffType       = s.RankBuffType,
        c.FlushSequence      = s.FlushSequence,
        c.UpdatedAtUtc       = SYSUTCDATETIME()
    FROM game.Characters AS c
             JOIN @Progress AS s ON s.CharacterId = c.CharacterId
    WHERE s.FlushSequence > c.FlushSequence; -- idempotence guard
END;
GO

-- 5. Le flush terminal -- deconnexion (GameConnectionHost.cs:249) et changement de zone
--    (ZoneMoveService.cs:174 et :259), tous trois via PositionWriteBehindHost.FlushCharacterNowAsync.
--    Progression et position dans le meme UPDATE : ce chemin n'a pas de "cycle suivant" pour rattraper une
--    moitie perdue. C'est ce flush-la qui fait survivre la liste d'auto-buff et le palier de rank-buff au
--    changement de zone : chaque zone a son propre port et l'entree en monde relit integralement la DB.
CREATE PROCEDURE game.usp_Character_PersistFinalFlush @Progress game.tvp_CharacterProgress READONLY,
                                                      @Position game.tvp_CharacterPosition READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE c
    SET c.Level              = p.Level,
        c.Level2             = p.Level2,
        c.Experience         = p.Experience,
        c.Life               = p.Life,
        c.MaxLife            = p.MaxLife,
        c.Mana               = p.Mana,
        c.MaxMana            = p.MaxMana,
        c.StatVit            = p.StatVit,
        c.StatStr            = p.StatStr,
        c.StatInt            = p.StatInt,
        c.StatDex            = p.StatDex,
        c.StatPoints         = p.StatPoints,
        c.SkillPoints        = p.SkillPoints,
        c.ContributionPoints = p.ContributionPoints,
        c.Exp2               = p.Exp2,
        c.RebirthCount       = p.RebirthCount,
        c.EatLifePotion      = p.EatLifePotion,
        c.EatManaPotion      = p.EatManaPotion,
        c.EatStrPotion       = p.EatStrPotion,
        c.EatDexPotion       = p.EatDexPotion,
        c.EatElePotion       = p.EatElePotion,
        c.DropItemTime       = p.DropItemTime,
        c.M15PetLuckyBoxPity = p.M15PetLuckyBoxPity,
        c.MountItemId        = p.MountItemId,
        c.MountExpActivity   = p.MountExpActivity,
        c.MountPower         = p.MountPower,
        c.MountSlotIndex     = p.MountSlotIndex,
        c.MountTime          = p.MountTime,
        c.VisibleState       = p.VisibleState,
        c.SpecialState       = p.SpecialState,
        c.UseOrnament        = p.UseOrnament,
        c.PetExpX2Time       = p.PetExpX2Time,
        c.AnimalAbsorbTime   = p.AnimalAbsorbTime,
        c.AnimalAbsorbState  = p.AnimalAbsorbState,
        c.CostumeIndex       = p.CostumeIndex,
        c.AutoBuffTime       = p.AutoBuffTime,
        c.AutoBuffSkill      = p.AutoBuffSkill,
        c.RankPointDate      = p.RankPointDate,
        c.RankBuffType       = p.RankBuffType,
        c.MapId              = q.MapId,
        c.PosX               = q.PosX,
        c.PosY               = q.PosY,
        c.PosZ               = q.PosZ,
        c.Heading            = q.Heading,
        c.FlushSequence      = q.FlushSequence,
        c.UpdatedAtUtc       = SYSUTCDATETIME()
    FROM game.Characters AS c
             JOIN @Progress AS p ON p.CharacterId = c.CharacterId
             JOIN @Position AS q ON q.CharacterId = c.CharacterId
    WHERE q.FlushSequence > c.FlushSequence; -- idempotence guard
END;
GO

-- 6. Le chemin de LECTURE. RS0 est append-only en queue : les trois nouvelles colonnes se posent APRES
--    CostumeIndex (dernier ajout, Migrations/007), jamais au milieu -- le mapper CaeriusNet lit par ORDINAL,
--    et une insertion mediane
--    decalerait toute la projection. AutoBuffTime etait deja projete a sa place historique (entre TeacherPoint
--    et PremiumExpireUtc) et n'est pas deplace : seul son cablage vers PlayerRuntimeState manquait. Le prefixe
--    stable de 19 colonnes partage avec usp_Character_GetForWorldEntrySummary est intact, et les quatre autres
--    resultats sont inchanges.
CREATE OR ALTER PROCEDURE game.usp_Character_GetForWorldEntry @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT c.CharacterId,
           c.AccountId,
           c.Slot,
           c.Name,
           c.Tribe,
           c.Gender,
           c.HeadType,
           c.FaceType,
           c.Level,
           c.MapId,
           c.PosX,
           c.PosY,
           c.PosZ,
           c.Heading,
           c.Life,
           c.MaxLife,
           c.Mana,
           c.MaxMana,
           c.FlushSequence,
           c.Experience,
           c.Level2,
           c.StatVit,
           c.StatStr,
           c.StatInt,
           c.StatDex,
           c.StatPoints,
           c.SkillPoints,
           c.Money,
           c.BigMoney,
           c.StoreMoney,
           c.BigStoreMoney,
           c.RebirthCount,
           c.Title,
           c.Halo,
           c.ContributionPoints,
           c.EatLifePotion,
           c.EatManaPotion,
           c.EatStrPotion,
           c.EatDexPotion,
           c.EatElePotion,
           c.ProtectForDeath,
           c.ProtectForDestroy,
           c.DoubleExpTime1,
           c.DoubleExpTime2,
           c.DropItemTime,
           c.InventoryDate,
           c.StoreDate,
           ISNULL(q.StepPermanent, 0) AS QuestStepPermanent,
           ISNULL(q.ActiveQuestId, 0) AS QuestActiveId,
           ISNULL(q.QSort, 0)         AS QuestSort,
           ISNULL(q.TargetPhase, 0)   AS QuestTargetPhase,
           ISNULL(q.KillCounter, 0)   AS QuestKillCounter,
           c.JoinWar,
           c.MissionKillOtherTribe,
           c.MissionKillMonster,
           c.MissionPlayTime,
           c.AutoHuntEnabled,
           c.AutoHuntConfig,
           c.AutoLifeRatio,
           c.AutoManaRatio,
           c.PetGrowth,
           c.PetActivity,
           c.TeacherPoint,
           c.AutoBuffTime,
           c.PremiumExpireUtc,
           c.Exp2,
           c.PreviousTribe,
           c.MountItemId,
           c.MountExpActivity,
           c.MountPower,
           c.MountSlotIndex,
           c.MountTime,
           c.AutoTime2,
           c.Zone241Time,
           c.PetBagDate,
           c.WarPoint,
           c.M15PetLuckyBoxPity,
           c.VisibleState,
           c.SpecialState,
           c.UseOrnament,
           c.BloodCoin,
           c.PetExpX2Time,
           c.AnimalAbsorbTime,
           c.AnimalAbsorbState,
           c.CostumeIndex,
           c.AutoBuffSkill,
           c.RankPointDate,
           c.RankBuffType
    FROM game.Characters AS c
             LEFT JOIN game.CharacterQuests AS q
                       ON q.CharacterId = c.CharacterId
    WHERE c.CharacterId = @CharacterId;

    SELECT Container,
           Slot,
           ItemId,
           CAST(Quantity AS INT) AS Quantity,
           Enchant,
           Combine,
           Refine,
           Socket,
           SocketGem1,
           SocketGem2,
           SocketGem3,
           ExpireDate,
           Serial
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId
    ORDER BY Container, Slot;

    SELECT SlotIndex,
           SkillId,
           Grade
    FROM game.CharacterSkills
    WHERE CharacterId = @CharacterId
    ORDER BY SlotIndex;

    SELECT Page,
           KeyIndex,
           Sort,
           Value1,
           Value2
    FROM game.CharacterHotkeys
    WHERE CharacterId = @CharacterId
    ORDER BY Page, KeyIndex;

    SELECT SlotIndex,
           Value,
           RemainingLegacyTicks
    FROM game.CharacterBuffs
    WHERE CharacterId = @CharacterId
    ORDER BY SlotIndex;
END;
GO
