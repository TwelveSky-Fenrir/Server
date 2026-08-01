-- Lot 6 -- penderie de costumes et soldes de compagnon : cinq champs mutes en jeu, persistes par le legacy,
-- et ecrits nulle part cote Fenrir.
--
-- NUMEROTATION : 007 et non 006 parce que trois passes concurrentes de la meme session ont deja pris 006
-- (006_avatar_state_flags_writeback, 006_character_autobuff_rankbuff_bottleindex_writeback,
-- 006_character_progress_cosmetics_and_currency_grants) -- meme convention que
-- Migrations/Seed/admin/024_error_catalog_heroranking_claim_reward.sql, numerote au-dessus du numero libre
-- suivant pour la meme raison. VOIR L'AVERTISSEMENT DE SEQUENCAGE en fin d'en-tete.
--
-- CE QUE PERSISTE LE LEGACY, ET OU. Les cinq champs sont dans AVATAR_INFO, montes par CreateAvatarColumn,
-- donc lus au SELECT et reecrits a l'UPDATE dans le meme aller-retour que le reste de l'avatar
-- (Server/Header/CSQLAvatar.cpp:769-794) :
--   aPetExpX2Time       STRUCT.h:438 (commente aDoublePatExpTime)  CSQLAvatar.cpp:640
--                       colonne reelle int(11) DEFAULT 0, Server/BuildEU33/DB/nxtserver.sql:126.
--                       Le decompte minute par minute ecrit SUR LA STRUCT SAUVEGARDEE
--                       (avt->aPetExpX2Time--, Server/ts25zone/S07_MyGame04.cpp:949) et diffuse
--                       S044DOUBLE_PET_EXP2 (S07_MyGame04.cpp:950) : le solde restant survit au relog.
--                       Republie au roster de login (Server/ts25login/S05_MyTransfer.cpp:426).
--   aAnimalAbsorbTime   STRUCT.h:501  CSQLAvatar.cpp:650  nxtserver.sql:157
--                       solde ACHETE : credite a l'usage des items 613/1222 [Mount Absorb(S)/(L)] par
--                       wCheckAdd + wAvatar.aAnimalAbsorbTime += tAddTime
--                       (Server/ts25zone/S04_MyWork03.cpp:3365-3378), decremente par tick sur la meme struct
--                       sauvegardee (S07_MyGame04.cpp:1246) et clampe a 0 (S07_MyGame04.cpp:1249).
--   aAnimalAbsorbState  STRUCT.h:502  CSQLAvatar.cpp:651  nxtserver.sql:158
--                       ATTENTION AU DOUBLON DE NOM : il existe DEUX aAnimalAbsorbState.
--                       (1) AVATAR_INFO::aAnimalAbsorbState (STRUCT.h:502) est l'autoritatif, c'est lui qui
--                           est persiste, et c'est lui qu'on persiste ici.
--                       (2) OBJECT_FOR_AVATAR::aAnimalAbsorbState (STRUCT.h:778) n'est qu'un miroir de
--                           diffusion, re-seede depuis le persiste a la charge
--                           (Server/ts25zone/S04_MyWork02.cpp:1000) et jamais sauvegarde.
--                       Les deux sont toujours ecrits en paire (S07_MyGame04.cpp:1254-1255).
--   aCostume[10]        STRUCT.h:443  CSQLAvatar.cpp:429-433 puis FIELD_AVATAR1 CSQLAvatar.cpp:701
--                       texte a champs fixes varchar(50) = 5 caracteres x 10 slots (nxtserver.sql:127),
--                       largeur confirmee par MAX_COSTUME_TEXT_LENGTH = (5 * MAX_AVATAR_COSTUME_NUM) + 1
--                       (Server/Header/CSQLDatabase.h:63,112), MAX_AVATAR_COSTUME_NUM = 10
--                       (Server/Header/Protocol/DEFINE.h:341).
--                       DEUX TABLEAUX PARALLELES SUIVENT LA MEME REGLE ET SONT AUSSI PERSISTES :
--                       aCostumeDate (CSQLAvatar.cpp:432,702 ; varchar(80), nxtserver.sql:129) et
--                       aCostumeExpireDate (CSQLAvatar.cpp:521,721 ; varchar(80), nxtserver.sql:183).
--                       Persister la penderie sans eux perdrait l'enchant (GetEnchantCostumeValue1,
--                       Server/Header/function.h:2086-2092) et l'expiration : les trois sont donc portes
--                       ensemble ici, pas le seul itemId.
--   aCostumeIndex       STRUCT.h:446  CSQLAvatar.cpp:661  nxtserver.sql:128 (DEFAULT -1)
--
-- L'ECART FENRIR, MESURE
--   * PetExpX2Time : mute par Zone.EconomyMirrors.cs (commande de charge des pilules) et decremente par
--     Domain/Simulation/PetExpBoostCountdownSystem.cs -- aucune colonne, aucun TVP, aucune procedure.
--     Le parametre PlayerEnterData.PetExpX2Time existait deja mais n'etait fourni par personne : le chemin de
--     lecture etait mort lui aussi.
--   * AnimalAbsorbState : mute par Zone.CosmeticMirrors.cs et remis a 0 par MountExpiryCountdownSystem --
--     aucune colonne. Non derivable : aucune autre colonne ne porte l'absorption.
--   * AnimalAbsorbTime : lu par MountAbsorbService pour AUTORISER l'absorption, et JAMAIS affecte nulle part
--     dans src/. Il est persiste ici parce que c'est un solde achete, mais tant que le credit a l'usage des
--     items 613/1222 (S04_MyWork03.cpp:3365-3378) n'est pas porte, il fera un aller-retour a 0. Ce script
--     ferme le trou de persistance, pas le trou de gameplay -- ce dernier est un lot a part.
--   * CostumeWardrobe : game.CharacterCostumes EXISTAIT, etait LUE par usp_Character_GetAccountRoster et
--     supprimee en cascade par usp_Character_Delete, mais AUCUNE procedure n'y faisait jamais INSERT ni
--     UPDATE ni MERGE. Les costumes obtenus n'etaient ecrits nulle part.
--   * CostumeIndex : la colonne game.Characters.CostumeIndex existait, n'etait lue que par
--     usp_Character_GetAccountRoster, jamais ecrite (0 UPDATE), et jamais chargee dans PlayerRuntimeState.
--
-- CE QUE FAIT CE SCRIPT, SECTION PAR SECTION
--
-- 1. game.Characters gagne PetExpX2Time / AnimalAbsorbTime / AnimalAbsorbState (INT, DEFAULT 0). Valeur sure
--    pour les lignes existantes : un solde inconnu vaut zero, jamais un credit offert.
--
-- 2. CK_Characters_CostumeIndex passe de BETWEEN -1 AND 9 a BETWEEN -1 AND 19. La contrainte de base etait
--    trop etroite d'un facteur decisif : aCostumeIndex PORTE l'etat porte / range. Equiper fait
--    +MAX_AVATAR_COSTUME_NUM (Server/ts25zone/S04_MyWork02.cpp:12402), retirer fait -MAX_AVATAR_COSTUME_NUM
--    (S04_MyWork02.cpp:12421), les deux sur wAvatar donc sur la struct sauvegardee, et la borne haute valide
--    est MAX_AVATAR_COSTUME_NUM2 = 19 (DEFINE.h:342, teste tel quel en S04_MyWork02.cpp:12414). Fenrir encode
--    deja exactement pareil (Domain/Costumes/CostumeStateResolver.cs:20-22, SlotCount = 10 / WornMax = 19).
--    Sans cet elargissement, la toute PREMIERE ecriture d'un costume porte violerait la contrainte.
--
-- 3. game.CharacterCostumes gagne ItemDate et ExpireDate (miroirs de aCostumeDate / aCostumeExpireDate).
--    ALTER ADD et non DROP+CREATE : la table porte une FK depuis game.Characters et de vraies lignes.
--
-- 4. Nouveau type game.tvp_CharacterCostumeSlot : la penderie complete d'un lot de personnages, SLOTS OCCUPES
--    SEULEMENT (absence de ligne = slot vide, meme convention que game.CharacterRunes /
--    game.tvp_CharacterRuneSocket). Il porte CharacterId, la ou tvp_CharacterRuneSocket s'en passe, parce que
--    le flush write-behind est un lot alors que usp_Character_PersistRunes est mono-personnage.
--
-- 5. game.tvp_CharacterProgress gagne quatre colonnes INT en QUEUE et les deux procedures de write-behind
--    gagnent les clauses SET correspondantes PLUS le remplacement de la penderie -- donc LE CHEMIN
--    D'ECRITURE DEJA EN PLACE, flush periodique (usp_Character_PersistProgressBatch) comme deconnexion et
--    changement de zone (usp_Character_PersistFinalFlush, seul appelant :
--    PositionWriteBehindHost.FlushCharacterNowAsync). Pas de second chemin d'ecriture : deux chemins sur la
--    meme entite finissent par diverger.
--
--    LE REMPLACEMENT DE PENDERIE EST BORNE PAR @Applied, PAS PAR @Progress. Le DELETE ne touche que les
--    personnages dont la ligne de progression a REELLEMENT ete appliquee (garde d'idempotence FlushSequence
--    strictement croissante), collectes par OUTPUT inserted.CharacterId. Un rejeu tardif, ou une livraison
--    hors ordre, n'applique alors rien du tout -- ni les scalaires ni la penderie : l'ensemble reste
--    idempotent d'un seul bloc, sur la meme sequence par entite.
--    COROLLAIRE LOAD-BEARING POUR L'APPELANT : @Costumes doit TOUJOURS porter la penderie COMPLETE de chaque
--    personnage present dans @Progress. Une penderie vide s'exprime par zero ligne -- le parametre TVP est
--    alors omis, SQL Server refusant un TVP a zero ligne -- et le DELETE seul la vide. Meme regle
--    "liste vide = effacement delibere" que game.usp_CharacterItems_ReplaceContainer et
--    game.usp_Character_PersistRunes. Cote C# le parametre est REQUIS, sans valeur par defaut, pour que le
--    compilateur interdise a un site d'appel de l'oublier silencieusement et de vider toutes les penderies.
--
--    Les trois instructions passent en transaction explicite (elles etaient mono-instruction) : une panne
--    entre le DELETE et l'INSERT ne doit jamais pouvoir committer le DELETE seul et vider une penderie.
--
-- 6. game.usp_Character_GetForWorldEntry gagne les quatre memes scalaires en QUEUE de RS0 -- le chemin de
--    LECTURE. Un champ persiste mais jamais recharge est pire qu'un champ non persiste : il donne l'illusion
--    de fonctionner. RS0 est append-only en queue par contrat (en-tete du fichier de base) et
--    CharacterWorldSnapshotDto est mappe par ordinal, donc l'ajout en queue est la seule forme sure. Le
--    prefixe stable de 19 colonnes partage avec usp_Character_GetForWorldEntrySummary est intact ; cette
--    procedure-la n'est pas touchee.
--
-- 7. Nouvelle game.usp_Character_GetCostumes : lecture autonome de la penderie a l'entree monde, calquee sur
--    game.usp_Character_GetRunes -- meme choix delibere de NE PAS l'ajouter en 6e result set du bundle chaud,
--    mappe par ordinal, de usp_Character_GetForWorldEntry.
--
-- POURQUOI UN NOUVEAU SCRIPT PLUTOT QU'UNE EDITION DES FICHIERS DE BASE
-- Tables/game/Characters.sql, Tables/game/CharacterCostumes.sql, Schemas/Types/game/tvp_CharacterProgress.sql,
-- StoredProcedures/game/usp_Character_PersistProgressBatch.sql, .../usp_Character_PersistFinalFlush.sql et
-- .../usp_Character_GetForWorldEntry.sql sont deja listes dans _manifest.txt, donc journalises par SHA-256 par
-- Fenrir.Tools.DbMigrator sur toute base les ayant appliques une fois (le volume de dev AppHost est
-- persistant). Le migrateur refuse de re-appliquer un chemin journalise dont le contenu a change : une
-- edition en place ferait echouer durement la prochaine execution. Une base FRAICHE applique les scripts de
-- base puis les migrations dans l'ordre du manifeste ; une base PERSISTANTE saute les scripts deja
-- journalises. Les deux convergent. Un TABLE type ne s'ALTER pas pour gagner des colonnes et ne se DROP pas
-- tant qu'une procedure le prend en parametre : les deux procedures sont donc droppees d'abord, exactement la
-- mecanique de Migrations/002. Les permissions EXECUTE sont inchangees, Schemas/002_roles.sql accordant
-- EXECUTE AU NIVEAU DU SCHEMA (GRANT EXECUTE ON SCHEMA::game TO fenrir_game_role) -- permission couvrante
-- heritee par tout objet cree dans le schema, maintenant et a venir.
--
-- AVERTISSEMENT DE SEQUENCAGE -- A LIRE AVANT DE FUSIONNER
-- Plusieurs passes concurrentes de la meme session etendent game.tvp_CharacterProgress en parallele, et
-- CHACUNE le DROPpe et le RECREE depuis la forme qu'elle avait sous les yeux. LE DERNIER SCRIPT APPLIQUE
-- GAGNE et efface silencieusement les colonnes des autres : le build resterait vert et la persistance
-- disparaitrait a l'execution. C'est le seul angle mort reel de ce lot.
-- Ce script reconcilie ce qu'il pouvait voir : son type est l'UNION, dans l'ordre exact du record
-- Fenrir.Data.Abstractions.Characters.CharacterProgressTvp au moment de l'ecriture, soit les 30 colonnes de
-- Migrations/002, + VisibleState/SpecialState/UseOrnament (006_avatar_state_flags_writeback),
-- + Title/Halo/TeacherPoint/WarPointDelta/BloodCoinDelta (006_character_progress_cosmetics_and_currency_
-- grants, WarPoint et BloodCoin credites RELATIVEMENT pour composer avec usp_Character_BuyWarPointItem /
-- usp_Character_SpendBloodCoinAndReplaceContainer au lieu de les racer), + les quatre de ce lot. Soit 42.
-- D'AUTRES SCRIPTS NUMEROTES 008 EXISTENT DEJA sur disque (compteurs d'avatar, auto-buff / rank-buff /
-- index de bouteille) et s'appliqueront APRES celui-ci : s'ils recreent le type sans ces 42 colonnes, ce sont
-- eux qui gagneront. La fusion doit produire UN SEUL script terminal portant l'union complete, dans le meme
-- ordre que le record CharacterProgressTvp fusionne, et le meme raisonnement vaut colonne par colonne pour
-- la projection RS0 de usp_Character_GetForWorldEntry, mappee par ordinal sur CharacterWorldSnapshotDto.

-- ---------------------------------------------------------------------------
-- 1. game.Characters : les trois soldes scalaires manquants.
-- ---------------------------------------------------------------------------
-- Garde d'idempotence sur la premiere colonne : les trois sont ajoutees dans le meme ALTER, elles sont donc
-- soit toutes presentes soit toutes absentes.
IF COL_LENGTH('game.Characters', 'PetExpX2Time') IS NULL
    ALTER TABLE game.Characters
        ADD PetExpX2Time INT NOT NULL
                CONSTRAINT DF_Characters_PetExpX2Time DEFAULT 0,
            AnimalAbsorbTime INT NOT NULL
                CONSTRAINT DF_Characters_AnimalAbsorbTime DEFAULT 0,
            AnimalAbsorbState INT NOT NULL
                CONSTRAINT DF_Characters_AnimalAbsorbState DEFAULT 0;
GO

-- ---------------------------------------------------------------------------
-- 2. Elargir CK_Characters_CostumeIndex a la plage legacy complete -1 .. 19.
-- ---------------------------------------------------------------------------
IF EXISTS (SELECT 1
           FROM sys.check_constraints
           WHERE name = N'CK_Characters_CostumeIndex'
             AND parent_object_id = OBJECT_ID(N'game.Characters'))
    ALTER TABLE game.Characters
        DROP CONSTRAINT CK_Characters_CostumeIndex;
GO

ALTER TABLE game.Characters
    ADD CONSTRAINT CK_Characters_CostumeIndex CHECK (CostumeIndex BETWEEN -1 AND 19); -- 10..19 = porte, Server/ts25zone/S04_MyWork02.cpp:12402 (+10) et :12414 (borne MAX_AVATAR_COSTUME_NUM2)
GO

-- ---------------------------------------------------------------------------
-- 3. game.CharacterCostumes : aCostumeDate et aCostumeExpireDate.
-- ---------------------------------------------------------------------------
IF COL_LENGTH('game.CharacterCostumes', 'ItemDate') IS NULL
    ALTER TABLE game.CharacterCostumes
        ADD ItemDate INT NOT NULL
                CONSTRAINT DF_CharacterCostumes_ItemDate DEFAULT 0,
            ExpireDate INT NOT NULL
                CONSTRAINT DF_CharacterCostumes_ExpireDate DEFAULT 0;
GO

-- ---------------------------------------------------------------------------
-- 4. Type de la penderie transmise au flush.
-- ---------------------------------------------------------------------------
-- Slots OCCUPES seulement : absence de ligne = slot vide. ItemDate empaquete enchant/combine/refine/socket
-- (ItemValueCodec, meme entier que CostumeZoneCommand.GrantedCostumeDate) ; ExpireDate est l'entier legacy
-- YYYYMMDD verbatim, 0 = pas une location, meme convention que game.CharacterItems.ExpireDate.
IF TYPE_ID(N'game.tvp_CharacterCostumeSlot') IS NULL
    EXEC (N'
CREATE TYPE game.tvp_CharacterCostumeSlot AS TABLE
(
    CharacterId INT     NOT NULL,
    Slot        TINYINT NOT NULL,
    ItemId      INT     NOT NULL,
    ItemDate    INT     NOT NULL,
    ExpireDate  INT     NOT NULL
);');
GO

-- ---------------------------------------------------------------------------
-- 5. tvp_CharacterProgress + les deux procedures de flush write-behind.
-- ---------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS game.usp_Character_PersistFinalFlush;
DROP PROCEDURE IF EXISTS game.usp_Character_PersistProgressBatch;
DROP TYPE IF EXISTS game.tvp_CharacterProgress;
GO

-- Forme de Migrations/006_avatar_state_flags_writeback (33 colonnes) + les quatre nouvelles en queue.
-- Meme raisonnement que DropItemTime / Eat*Potion / Mount* : sans elles ici, le flush ecraserait
-- silencieusement un compteur vivant par sa derniere valeur persistee au cycle suivant.
-- Les quatre restent INT bien que le legacy borne CostumeIndex a -1..19 : le TVP est une forme transitoire
-- cote client, pas une ligne stockee, et CK_Characters_CostumeIndex fait foi a l'ecriture.
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
    CostumeIndex       INT      NOT NULL
);
GO

-- Idempotent : meme garde FlushSequence strictement croissante que game.usp_Character_PersistBatch (une seule
-- sequence monotone par personnage, partagee par les deux saveurs de flush).
-- Money/BigMoney volontairement absents : les soldes ne bougent que par leurs procedures atomiques dediees,
-- pour qu'un flush last-write-wins ne puisse jamais ecraser un solde. Les quatre nouvelles colonnes sont dans
-- la meme categorie que DropItemTime/Eat*Potion/Mount* : etat mono-proprietaire de PlayerRuntimeState.
-- @Costumes : penderie COMPLETE de chaque personnage present dans @Progress, slots occupes seulement, zero
-- ligne = penderie vide (parametre alors omis par l'appelant).
CREATE PROCEDURE game.usp_Character_PersistProgressBatch @Progress game.tvp_CharacterProgress READONLY,
                                                         @Costumes game.tvp_CharacterCostumeSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Applied TABLE (CharacterId INT NOT NULL PRIMARY KEY);

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

-- Contrepartie mono-personnage, utilisee uniquement par le flush immediat de deconnexion ET de changement de
-- zone (PositionWriteBehindHost.FlushCharacterNowAsync ; une transition de zone est une reconnexion complete
-- cote client, ZoneMoveService, donc la base est le seul porteur d'etat entre les deux zones). Progression +
-- position + penderie dans la meme transaction : les morceaux d'un instantane de deconnexion ne peuvent pas
-- etre separes par une panne en milieu de sequence. Meme garde d'idempotence ; les deux TVP scalaires portent
-- une ligne chacun, estampillee de la meme FlushSequence par l'appelant.
CREATE PROCEDURE game.usp_Character_PersistFinalFlush @Progress game.tvp_CharacterProgress READONLY,
                                                      @Position game.tvp_CharacterPosition READONLY,
                                                      @Costumes game.tvp_CharacterCostumeSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Applied TABLE (CharacterId INT NOT NULL PRIMARY KEY);

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

-- ---------------------------------------------------------------------------
-- 6. Chemin de LECTURE : les quatre scalaires en queue de RS0.
-- ---------------------------------------------------------------------------
-- RS0 est append-only en queue (regle posee par l'en-tete du fichier de base) : PetExpX2Time /
-- AnimalAbsorbTime / AnimalAbsorbState / CostumeIndex se posent apres VisibleState/SpecialState/UseOrnament
-- (Migrations/006_avatar_state_flags_writeback) puis BloodCoin
-- (Migrations/006_character_progress_cosmetics_and_currency_grants), eux-memes poses apres
-- M15PetLuckyBoxPity. Jamais au milieu. L'ordre reproduit exactement la queue de
-- CharacterWorldSnapshotDto, mappe par ordinal, telle que les passes concurrentes l'ont laissee.
-- CREATE OR ALTER, meme posture que ce dernier. Le prefixe stable de 19 colonnes partage avec
-- usp_Character_GetForWorldEntrySummary est intact. Les quatre autres result sets sont repris verbatim.
-- La penderie n'est deliberement PAS un 6e result set ici : elle se lit par game.usp_Character_GetCostumes,
-- meme raisonnement que game.usp_Character_GetRunes -- garder intact ce bundle chaud mappe par ordinal.
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
           c.CostumeIndex
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

-- ---------------------------------------------------------------------------
-- 7. Chemin de LECTURE : la penderie.
-- ---------------------------------------------------------------------------
-- Lecture autonome de la penderie a l'entree monde, hydratee dans PlayerRuntimeState.CostumeWardrobe /
-- CostumeDate / CostumeExpireDate. Deliberement SEPAREE du bundle a 5 result sets de
-- usp_Character_GetForWorldEntry, meme raisonnement que game.usp_Character_GetRunes.
-- Une ligne par slot OCCUPE (Slot 0..9) ; les slots absents sont hydrates a (0, 0, 0) par l'appelant.
-- Forme memoire legacy : wAvatar.aCostume[10] / aCostumeDate[10] / aCostumeExpireDate[10]
-- (Server/Header/Protocol/STRUCT.h:443-445).
CREATE OR ALTER PROCEDURE game.usp_Character_GetCostumes @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Slot,
           ItemId,
           ItemDate,
           ExpireDate
    FROM game.CharacterCostumes
    WHERE CharacterId = @CharacterId
    ORDER BY Slot;
END;
GO
