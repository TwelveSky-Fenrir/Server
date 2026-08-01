-- Lot 5 -- six compteurs mutes en jeu, persistes par le legacy, perdus par Fenrir.
--
-- CE QUE FAIT LE LEGACY (verifie ligne a ligne)
-- Les six ont une colonne dediee dans la table avatar, montee par CreateAvatarColumn, donc relue au SELECT et
-- reecrite au meme UPDATE d'avatar que le reste de l'AVATAR_INFO (Server/ts25playuser/S08_MyDB.cpp:99
-- GetAvatar(UPDATE, mDBTable[2], ...)) :
--
--   aAutoTime        Server/Header/Protocol/STRUCT.h:463 ; Server/Header/CSQLAvatar.cpp:641 FIELD_AVATAR0,
--                    soit CREATE_FIELD(0, ...) = int (Server/Header/CSQLAvatar.cpp:550).
--                    Budget d'auto-chasse exprime en DATE LIMITE YYYYMMDD, diffuse par S061AUTO_HUNT_DAY.
--                    Credit par objet : Server/ts25zone/S04_MyWork03.cpp:3782-3788 (ReturnAddDate).
--                    Expiration en jeu : Server/ts25zone/S07_MyGame04.cpp:790-796.
--   aAutoTime2       STRUCT.h:464 ; CSQLAvatar.cpp:642 FIELD_AVATAR0.
--                    Budget en MINUTES reelles, decremente une fois par minute quand aAutoTime est epuise
--                    (Server/ts25zone/S07_MyGame04.cpp:798-809, S062AUTO_HUNT_HOUR) ; l'epuisement des deux
--                    declenche B_RETURN_TO_AUTO_ZONE (S07_MyGame04.cpp:820). 1440 a la creation
--                    (Server/ts25login/S04_MyWork02.cpp:888, sous #ifdef LNW33).
--   aBuffX2Time      STRUCT.h:435 (alias aKiTime) ; CSQLAvatar.cpp:638 FIELD_AVATAR0.
--                    Duree restante du doublement de duree de buff, decrementee par minute reelle
--                    (Server/ts25zone/S07_MyGame04.cpp:1035-1042, S042DOUBLE_BUFF_TIME, sinon
--                    SetUserBonus2()) ; creditee par consommable (Server/ts25zone/S04_MyWork03.cpp:3066,
--                    objet 1132, +60). Aucune ligne SetIntegerLow de S07_MyGame03.cpp:5691-5698 ne le touche.
--   aPremium         STRUCT.h:537-541. USE_PREMIUM_LONGTIME est defini sans condition
--                    (Server/Header/Protocol/DEFINE.h:61), hors de tout bloc M33/LNW33, et n'est jamais
--                    #undef : la branche compilee est donc le #else, FIELD_AVATAR02(aPremium)
--                    (CSQLAvatar.cpp:681), soit CREATE_FIELD(2, ...) = long long (CSQLAvatar.cpp:551), une
--                    colonne BIGINT. L'echelle est un time_t Unix et non un YYYYMMDD : la maintenance fait
--                    time(&now) puis if (aPremium < now) aPremium = 0
--                    (Server/ts25zone/S07_MyGame04.cpp:1085-1092), et l'expiration au chargement compare de
--                    la meme facon (Server/ts25zone/S07_MyGame03.cpp:5549-5559). Citer la variante int
--                    aPremium / ReturnNowDate() serait citer du code mort.
--   aEquip[EPET][2]  croissance du familier. La signature Server/ts25zone/H08_MyGameSystem.h:196 nomme son 2e
--                    parametre pGrowUpValue et Server/Header/Protocol/MyFactor.cpp:3457 y passe
--                    aEquip[EPET][2] ; PETSYSTEM::ProcessForExperience fait aEquip[EPET][2] += pExperience
--                    (Server/ts25zone/GameSystem/GameSystem_07_Pet.cpp:1937). Persistance REELLE mais
--                    INDIRECTE : champ empaquete sur 9 chiffres sans clamp (EFIX_NONE) dans la colonne texte
--                    aEquip (Server/Header/CSQLAvatar.cpp:320), emise par FIELD_AVATAR1(aEquip)
--                    (CSQLAvatar.cpp:690).
--   aEquip[EPET][1]  activite du familier (lecture explicite
--                    Server/ts25zone/GameSystem/GameSystem_07_Pet.cpp:1919), plafond
--                    MAX_PAT_ACTIVITY_SIZE = 100 (DEFINE.h:610), pleine a la creation
--                    (Server/ts25login/S04_MyWork02.cpp:1130), decrementee d'une unite tous les 60 ticks tant
--                    que aPetExpX2Time < 1 (Server/ts25zone/S07_MyGame04.cpp:834-860). Meme colonne texte, 3
--                    chiffres, clamp EFIX_DUPLICATE (CSQLAvatar.cpp:319) qui laisse passer 0..100 sans perte.
--
-- LE BLOC D'AUTO-CHASSE EST BIEN COMPILE. FREE_HUNT est defini a Server/Header/Protocol/DEFINE.h:46, a
-- l'interieur de la branche #else de #ifdef M33 (DEFINE.h:21 / :25 / :49), exactement comme ses voisins
-- __GOD__ et __REBIRTH__. Les deux configurations livrees definissent M33
-- (Server/ts25latest_config.props:6 et :11) et aucune ne l'ajoute en PreprocessorDefinitions
-- (Server/ts25latest_general.props:15) : FREE_HUNT est donc ETEINT partout. Consequences :
--   * les gardes #ifndef FREE_HUNT de Server/ts25zone/S07_MyGame04.cpp:787 (decompte) et
--     Server/ts25zone/S07_MyGame03.cpp:5696 (normalisation au chargement) sont ACTIVES -- le budget est
--     reellement applique ;
--   * le #ifdef FREE_HUNT qui offrirait aAutoTime = 99999999 a la creation
--     (Server/ts25login/S04_MyWork02.cpp:881) ne l'est PAS -- d'ou DEFAULT 0 pour AutoTime ci-dessous, qui est
--     la valeur legacy d'un personnage neuf.
-- La normalisation au chargement n'est pas une remise a zero systematique :
-- SetIntegerLow(avt->aAutoTime, tNowDate, 0) (S07_MyGame03.cpp:5697) avec SetIntegerLow defini
-- Server/Header/function.h:242 comme "si tValue < tCheck alors tSet" ne remet a 0 qu'une date DEJA PASSEE ;
-- SetIntegerLow(avt->aAutoTime2, 1, 0) (:5698) ne fait que plancher a 0. Ces deux regles sont rejouees cote
-- Fenrir dans EnterWorldService, pas ici : la colonne garde la valeur brute.
--
-- L'ECART FENRIR
--   AutoTime           aucune colonne. PlayerRuntimeState.AutoHuntPaidDayBudget n'etait ecrit que par
--                      AutoHuntTickSystem : le budget-jour repartait a 0 a chaque entree en jeu.
--   AutoTime2          colonne presente et deja projetee par usp_Character_GetForWorldEntry, mais elle
--                      n'alimentait que le paquet AvatarInfo -- jamais PlayerRuntimeState, jamais reecrite.
--   BuffX2Time         aucune colonne. AddPlayerCommand.BuffX2Time existait avec un defaut 0 qu'aucun
--                      appelant ne fournissait, alors que la maintenance minute le decremente comme s'il
--                      etait charge.
--   PremiumExpireUtc   colonne presente ET relue, mais ecrite uniquement par
--                      usp_Character_CreateWithStarterKit : aucun achat de premium ne pouvait la prolonger
--                      durablement, et la remise a 0 a expiration n'etait pas persistee.
--   PetGrowth/         colonnes presentes ET relues, et il existait meme une procedure dediee,
--   PetActivity        game.usp_Character_SetPetGrowth -- sans AUCUN appelant applicatif. La croissance et
--                      l'activite du familier n'atteignaient donc jamais SQL.
-- Un budget paye qui ne survit pas a la deconnexion est exploitable a l'infini par relog ; nourrir un
-- familier coute des objets. Ce sont structurellement des champs persistants, pas des champs ephemeres.
--
-- CE QUE FAIT CE SCRIPT
-- 1. ajoute les deux colonnes reellement absentes, AutoTime et BuffX2Time ;
-- 2. ajoute six colonnes en QUEUE de game.tvp_CharacterProgress et les clauses SET correspondantes aux deux
--    procedures de write-behind -- donc au CHEMIN D'ECRITURE DEJA EN PLACE : flush periodique
--    (usp_Character_PersistProgressBatch), deconnexion et changement de zone
--    (usp_Character_PersistFinalFlush, unique appelant PositionWriteBehindHost.FlushCharacterNowAsync). Pas
--    de chemin parallele : c'est aussi pourquoi la procedure morte usp_Character_SetPetGrowth est droppee par
--    Migrations/009_drop_usp_character_setpetgrowth.sql plutot que reutilisee -- deux chemins d'ecriture sur
--    la meme entite finissent par diverger, exactement comme
--    Migrations/003_drop_usp_character_setmountprogression.sql l'a acte pour la monture ;
-- 3. ajoute AutoTime et BuffX2Time en QUEUE de la projection RS0 de usp_Character_GetForWorldEntry -- le
--    chemin de LECTURE. Un champ persiste mais jamais relu est pire qu'un champ non persiste : il donne
--    l'illusion de fonctionner. Les quatre autres (AutoTime2, PremiumExpireUtc, PetGrowth, PetActivity)
--    etaient deja projetes.
--
-- SUR QUOI CE SCRIPT SE REBASE -- A LIRE AVANT DE LE DEPLACER DANS LE MANIFESTE
-- Il reprend VERBATIM la forme laissee par Migrations/007_costume_and_companion_timer_writeback.sql, la
-- derniere migration enregistree au moment de l'ecriture : type a 42 colonnes (30 de Migrations/002, plus
-- VisibleState/SpecialState/UseOrnament, plus Title/Halo/TeacherPoint/WarPointDelta/BloodCoinDelta, plus
-- PetExpX2Time/AnimalAbsorbTime/AnimalAbsorbState/CostumeIndex), procedures a parametre @Costumes et
-- remplacement de penderie borne par OUTPUT inserted.CharacterId -- et y ajoute les six siennes EN QUEUE.
-- Le mapper TVP genere par CaeriusNet lie POSITIONNELLEMENT, donc l'ordre des colonnes est le contrat, et
-- c'est la DERNIERE migration appliquee qui doit porter l'union complete : ce script doit donc rester apres
-- Migrations/007 dans _manifest.txt, et toute migration ajoutee apres lui devra reprendre ces 48 colonnes.
-- Les regimes d'ecriture des colonnes reprises sont conserves a l'identique : absolu pour
-- Title/Halo/TeacherPoint et pour les quatre du lot costumes, RELATIF pour WarPoint/BloodCoin (X = X + delta),
-- qui possedent une voie de depense atomique concurrente.
-- Les six colonnes ajoutees ici sont absolues : PlayerRuntimeState en est l'unique proprietaire, aucune autre
-- procedure ne les ecrit -- meme categorie que DropItemTime/Eat*Potion/Mount*, pas celle de Money.
--
-- POURQUOI UN NOUVEAU SCRIPT PLUTOT QU'UNE EDITION DES FICHIERS DE BASE
-- Tables/game/Characters.sql, Schemas/Types/game/tvp_CharacterProgress.sql,
-- StoredProcedures/game/usp_Character_PersistProgressBatch.sql, usp_Character_PersistFinalFlush.sql et
-- usp_Character_GetForWorldEntry.sql sont deja listes dans _manifest.txt, donc journalises par SHA-256 par
-- Fenrir.Tools.DbMigrator sur toute base qui les a appliques une fois. Le migrateur refuse de re-appliquer un
-- chemin journalise dont le contenu a change : une edition en place ferait echouer durement la prochaine
-- execution. Une base FRAICHE applique les scripts de base puis les migrations dans l'ordre du manifeste ; une
-- base PERSISTANTE n'applique que les migrations manquantes. Les deux convergent.
--
-- POURQUOI ALTER ADD POUR LES COLONNES ET DROP+CREATE POUR LE TYPE
-- game.Characters porte de vraies donnees joueur : un ALTER additif avec DEFAULT sur une colonne NOT NULL est
-- une operation de metadonnees seule, lignes existantes preservees a 0 -- valeur sure ici, 0 signifiant
-- "aucun budget" pour AutoTime (une date deja passee serait de toute facon normalisee a 0 au chargement) et
-- "aucun temps restant" pour BuffX2Time. Un TABLE type, lui, ne s'ALTERe pas pour gagner des colonnes et ne
-- se droppe pas tant qu'une procedure le prend en parametre ; les deux procedures de write-behind sont donc
-- droppees d'abord, comme en Migrations/002. Les permissions EXECUTE sont inchangees : Schemas/002_roles.sql
-- accorde EXECUTE au niveau SCHEMA, permission couvrante heritee par tout objet cree dans le schema.
--
-- CE QUE CE SCRIPT NE FAIT PAS
-- Il ne touche pas usp_Character_GetAccountRoster (elle projette deja PetGrowth/PetActivity et le roster de
-- selection de personnage n'a pas besoin des budgets). Il ne touche pas usp_Character_GetForWorldEntrySummary
-- (prefixe stable de 19 colonnes, inchange). Il n'ajoute AUCUNE voie de credit : rien cote Fenrir ne credite
-- encore aAutoTime (objets 658/687/1217/8105) ni aBuffX2Time (objet 1132) ni le premium -- ce script rend
-- durable ce qui est deja mute, il n'invente pas de gain.

-- 1. Les deux colonnes reellement absentes. Gardes d'idempotence : le migrateur journalise, mais ces scripts
--    doivent rester rejouables a la main sur une base a l'etat inconnu.
IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'AutoTime')
ALTER TABLE game.Characters
    ADD AutoTime INT NOT NULL
        CONSTRAINT DF_Characters_AutoTime DEFAULT 0; -- aAutoTime (Server/Header/CSQLAvatar.cpp:641) : budget d'auto-chasse en DATE LIMITE YYYYMMDD, meme echelle qu'InventoryDate/StoreDate, PAS un compteur de minutes comme AutoTime2
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'BuffX2Time')
ALTER TABLE game.Characters
    ADD BuffX2Time INT NOT NULL
        CONSTRAINT DF_Characters_BuffX2Time DEFAULT 0; -- aBuffX2Time alias aKiTime (Server/Header/CSQLAvatar.cpp:638) : minutes restantes de doublement de duree de buff, decrement par minute reelle (Server/ts25zone/S07_MyGame04.cpp:1035-1042)
GO

IF NOT EXISTS (SELECT 1
               FROM sys.check_constraints
               WHERE name = N'CK_Characters_AutoTime')
ALTER TABLE game.Characters
    ADD CONSTRAINT CK_Characters_AutoTime CHECK (AutoTime >= 0);
GO

IF NOT EXISTS (SELECT 1
               FROM sys.check_constraints
               WHERE name = N'CK_Characters_BuffX2Time')
ALTER TABLE game.Characters
    ADD CONSTRAINT CK_Characters_BuffX2Time CHECK (BuffX2Time >= 0);
GO

-- 2. Dropper les deux procedures qui referencent le type (elles bloquent DROP TYPE), puis le type.
DROP PROCEDURE IF EXISTS game.usp_Character_PersistFinalFlush;
DROP PROCEDURE IF EXISTS game.usp_Character_PersistProgressBatch;
DROP TYPE IF EXISTS game.tvp_CharacterProgress;
GO

-- 3. Recreer le type : forme de Migrations/007 (42 colonnes) + les six nouvelles en queue.
-- PetActivity reste INT ici alors que game.Characters le porte en TINYINT : meme posture que
-- RebirthCount/M15PetLuckyBoxPity/Eat*Potion et que les Mount* de Migrations/002 -- ce TVP est une forme de
-- transport, pas une ligne stockee, et SQL Server convertit vers la colonne plus etroite a l'ecriture.
-- PremiumExpireUtc est BIGINT : time_t Unix, meme largeur que la colonne (aPremium en long long sous
-- USE_PREMIUM_LONGTIME) -- un INT deborderait en 2038.
CREATE TYPE game.tvp_CharacterProgress AS TABLE
(
    CharacterId        INT      NOT NULL,
    FlushSequence      BIGINT   NOT NULL,
    Level              SMALLINT NOT NULL,
    Level2             SMALLINT NOT NULL,
    Experience         BIGINT   NOT NULL,
    Life               INT      NOT NULL,
    MaxLife            INT      NOT NULL,
    Mana               INT      NOT NULL,
    MaxMana            INT      NOT NULL,
    StatVit            INT      NOT NULL,
    StatStr            INT      NOT NULL,
    StatInt            INT      NOT NULL,
    StatDex            INT      NOT NULL,
    StatPoints         INT      NOT NULL,
    SkillPoints        INT      NOT NULL,
    ContributionPoints INT      NOT NULL,
    Exp2               INT      NOT NULL,
    RebirthCount       INT      NOT NULL,
    EatLifePotion      INT      NOT NULL,
    EatManaPotion      INT      NOT NULL,
    EatStrPotion       INT      NOT NULL,
    EatDexPotion       INT      NOT NULL,
    EatElePotion       INT      NOT NULL,
    DropItemTime       INT      NOT NULL,
    M15PetLuckyBoxPity INT      NOT NULL,
    MountItemId        INT      NOT NULL,
    MountExpActivity   INT      NOT NULL,
    MountPower         INT      NOT NULL,
    MountSlotIndex     INT      NOT NULL,
    MountTime          INT      NOT NULL,
    VisibleState       INT      NOT NULL,
    SpecialState       INT      NOT NULL,
    UseOrnament        INT      NOT NULL,
    Title              INT      NOT NULL,
    Halo               INT      NOT NULL,
    TeacherPoint       INT      NOT NULL,
    WarPointDelta      INT      NOT NULL,
    BloodCoinDelta     INT      NOT NULL,
    PetExpX2Time       INT      NOT NULL,
    AnimalAbsorbTime   INT      NOT NULL,
    AnimalAbsorbState  INT      NOT NULL,
    CostumeIndex       INT      NOT NULL,
    AutoTime           INT      NOT NULL,
    AutoTime2          INT      NOT NULL,
    BuffX2Time         INT      NOT NULL,
    PremiumExpireUtc   BIGINT   NOT NULL,
    PetGrowth          INT      NOT NULL,
    PetActivity        INT      NOT NULL
);
GO

-- 4. Le flush periodique. Garde d'idempotence (FlushSequence strictement croissante) inchangee.
-- Money/BigMoney restent volontairement absents : les soldes ne bougent que par leurs procedures atomiques
-- dediees. WarPoint/BloodCoin restent RELATIFS pour la meme raison (ils ont une voie de depense concurrente).
-- Les six ajouts sont absolus : PlayerRuntimeState en est l'unique proprietaire.
-- @Costumes et le remplacement de penderie borne par @Applied sont repris verbatim de Migrations/007.
CREATE PROCEDURE game.usp_Character_PersistProgressBatch @Progress game.tvp_CharacterProgress READONLY,
                                                         @Costumes game.tvp_CharacterCostumeSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Applied TABLE
                     (
                         CharacterId INT NOT NULL PRIMARY KEY
                     );

    BEGIN TRANSACTION;

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
        c.Title              = s.Title,
        c.Halo               = s.Halo,
        c.TeacherPoint       = s.TeacherPoint,
        c.WarPoint           = c.WarPoint + s.WarPointDelta,
        c.BloodCoin          = c.BloodCoin + s.BloodCoinDelta,
        c.PetExpX2Time       = s.PetExpX2Time,
        c.AnimalAbsorbTime   = s.AnimalAbsorbTime,
        c.AnimalAbsorbState  = s.AnimalAbsorbState,
        c.CostumeIndex       = s.CostumeIndex,
        c.AutoTime           = s.AutoTime,
        c.AutoTime2          = s.AutoTime2,
        c.BuffX2Time         = s.BuffX2Time,
        c.PremiumExpireUtc   = s.PremiumExpireUtc,
        c.PetGrowth          = s.PetGrowth,
        c.PetActivity        = s.PetActivity,
        c.FlushSequence      = s.FlushSequence,
        c.UpdatedAtUtc       = SYSUTCDATETIME()
    OUTPUT inserted.CharacterId INTO @Applied (CharacterId)
    FROM game.Characters AS c
             JOIN @Progress AS s ON s.CharacterId = c.CharacterId
    WHERE s.FlushSequence > c.FlushSequence; -- idempotence guard

    DELETE cc
    FROM game.CharacterCostumes AS cc
             JOIN @Applied AS a ON a.CharacterId = cc.CharacterId;

    INSERT INTO game.CharacterCostumes (CharacterId, Slot, ItemId, ItemDate, ExpireDate)
    SELECT s.CharacterId,
           s.Slot,
           s.ItemId,
           s.ItemDate,
           s.ExpireDate
    FROM @Costumes AS s
             JOIN @Applied AS a ON a.CharacterId = s.CharacterId;

    COMMIT TRANSACTION;
END;
GO

-- 5. Le flush immediat de deconnexion ET de changement de zone. Progression + position dans un seul UPDATE :
--    les deux moities d'un instantane ne peuvent pas etre coupees par un echec en cours de sequence. Une
--    transition de zone est une reconnexion complete cote client (ZoneMoveService), donc ces colonnes sont le
--    seul porteur de l'etat entre les deux zones.
CREATE PROCEDURE game.usp_Character_PersistFinalFlush @Progress game.tvp_CharacterProgress READONLY,
                                                      @Position game.tvp_CharacterPosition READONLY,
                                                      @Costumes game.tvp_CharacterCostumeSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Applied TABLE
                     (
                         CharacterId INT NOT NULL PRIMARY KEY
                     );

    BEGIN TRANSACTION;

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
        c.Title              = p.Title,
        c.Halo               = p.Halo,
        c.TeacherPoint       = p.TeacherPoint,
        c.WarPoint           = c.WarPoint + p.WarPointDelta,
        c.BloodCoin          = c.BloodCoin + p.BloodCoinDelta,
        c.PetExpX2Time       = p.PetExpX2Time,
        c.AnimalAbsorbTime   = p.AnimalAbsorbTime,
        c.AnimalAbsorbState  = p.AnimalAbsorbState,
        c.CostumeIndex       = p.CostumeIndex,
        c.AutoTime           = p.AutoTime,
        c.AutoTime2          = p.AutoTime2,
        c.BuffX2Time         = p.BuffX2Time,
        c.PremiumExpireUtc   = p.PremiumExpireUtc,
        c.PetGrowth          = p.PetGrowth,
        c.PetActivity        = p.PetActivity,
        c.MapId              = q.MapId,
        c.PosX               = q.PosX,
        c.PosY               = q.PosY,
        c.PosZ               = q.PosZ,
        c.Heading            = q.Heading,
        c.FlushSequence      = q.FlushSequence,
        c.UpdatedAtUtc       = SYSUTCDATETIME()
    OUTPUT inserted.CharacterId INTO @Applied (CharacterId)
    FROM game.Characters AS c
             JOIN @Progress AS p ON p.CharacterId = c.CharacterId
             JOIN @Position AS q ON q.CharacterId = c.CharacterId
    WHERE q.FlushSequence > c.FlushSequence; -- idempotence guard

    DELETE cc
    FROM game.CharacterCostumes AS cc
             JOIN @Applied AS a ON a.CharacterId = cc.CharacterId;

    INSERT INTO game.CharacterCostumes (CharacterId, Slot, ItemId, ItemDate, ExpireDate)
    SELECT s.CharacterId,
           s.Slot,
           s.ItemId,
           s.ItemDate,
           s.ExpireDate
    FROM @Costumes AS s
             JOIN @Applied AS a ON a.CharacterId = s.CharacterId;

    COMMIT TRANSACTION;
END;
GO

-- 6. Le chemin de LECTURE. RS0 est append-only en queue (regle posee par l'en-tete du fichier de base) :
--    AutoTime et BuffX2Time se posent apres les colonnes deja ajoutees par les migrations precedentes, jamais
--    au milieu -- le lecteur genere pour CharacterWorldSnapshotDto lie par ORDINAL, donc l'ordre du SELECT est
--    le contrat. Le prefixe stable de 19 colonnes partage avec usp_Character_GetForWorldEntrySummary est
--    intact. Les quatre autres result sets sont repris verbatim.
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
           c.AutoTime,
           c.BuffX2Time
    FROM game.Characters AS c
             LEFT JOIN game.CharacterQuests AS q
                       ON q.CharacterId = c.CharacterId
    WHERE c.CharacterId = @CharacterId;

    SELECT Container,
           Slot,
           ItemId,
           CAST(Quantity AS INT) AS Quantity, -- game.CharacterItems.Quantity is SMALLINT; widen back to INT
           Enchant,                           -- here so CharacterItemSlotDto's existing int-typed ctor param
           Combine,                           -- keeps reading it via SqlDataReader.GetInt32 without an
           Refine,                            -- InvalidCastException (see CharacterItems.sql's own comment)
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
