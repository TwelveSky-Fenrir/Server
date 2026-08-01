-- Lot 2 -- VisibleState / SpecialState / UseOrnament : trois drapeaux d'avatar mutes en jeu, jamais ecrits.
--
-- CE QUE FAIT LE LEGACY (verifie ligne a ligne)
-- Les trois sont de vraies colonnes de la table avatar, montees par CreateAvatarColumn -- donc lues au SELECT
-- et reecrites a l'UPDATE dans le meme aller-retour que le reste de l'AVATAR_INFO :
--   Server/Header/CSQLAvatar.cpp:558 FIELD_AVATAR0(aVisibleState)
--   Server/Header/CSQLAvatar.cpp:559 FIELD_AVATAR0(aSpecialState)
--   Server/Header/CSQLAvatar.cpp:624 FIELD_AVATAR0(aUseOrnament)
-- Les champs eux-memes : Server/Header/Protocol/STRUCT.h:326-327 (aVisibleState, aSpecialState -- les deux
-- premiers champs d'AVATAR_INFO) et :417 (aUseOrnament, commentaire d'origine //aEquipEffectValue).
--
-- LE POINT DECISIF POUR aVisibleState / aSpecialState : IL Y A DEUX COPIES, ET LA PERSISTEE FAIT FOI.
-- Les commandes GM ecrivent SYSTEMATIQUEMENT les deux, la copie persistee (wAvatar = AVATAR_INFO) ET la copie
-- de diffusion (mDATA = OBJECT_FOR_AVATAR, Server/Header/Protocol/STRUCT.h:737-738) :
--   Server/ts25zone/S04_MyWork04.cpp:936-937  GM 501 HIDE    -> wAvatar.aVisibleState = 0 ; mDATA idem
--   Server/ts25zone/S04_MyWork04.cpp:949-950  GM 502 SHOW    -> 1 ; mDATA idem
--   Server/ts25zone/S04_MyWork04.cpp:1274     GM 511 EQUIP   -> mDATA.aSpecialState = wAvatar.aSpecialState = 1
--   Server/ts25zone/S04_MyWork04.cpp:1291     GM 512 UNEQUIP -> 0
-- et a l'entree en zone c'est la copie PERSISTEE qui repeuple la copie de diffusion, jamais l'inverse :
--   Server/ts25zone/S04_MyWork02.cpp:970-971
--     tUserInfo->mDATA.aVisibleState = wAvatar.aVisibleState;
--     tUserInfo->mDATA.aSpecialState = wAvatar.aSpecialState;
-- Le mode invisible GM survit donc a la deconnexion ET au changement de zone. La valeur initiale a la creation
-- est 1 (Server/ts25login/S04_MyWork02.cpp:741), d'ou le DEFAULT 1 deja porte par game.Characters.
--
-- aUseOrnament N'EST PAS UN DRAPEAU DE VUE : c'est l'interrupteur du bonus d'ornement.
--   Server/ts25zone/S04_MyWork02.cpp:11046  TRIBE_WORK sort 9  -> wAvatar.aUseOrnament = 1, puis
--                                                                 SetBasicAbilityFromEquip()
--   Server/ts25zone/S04_MyWork02.cpp:11053  TRIBE_WORK sort 10 -> 0, puis SetBasicAbilityFromEquip()
--   Server/ts25zone/S07_MyGame04.cpp:1276-1294  toutes les 120 ticks, si aUseOrnament == 1 : decrement de
--     aGoldTime ou aSilverTime selon MyFactor::IsUsedOrnament(), puis aUseOrnament = 0 quand les deux
--     compteurs sont epuises.
--   lecture pour le calcul de stats : Server/ts25zone/S04_MyWork03.cpp:3646 et :3661.
-- Il est couple a deux compteurs eux-memes persistes, aSilverTime et aGoldTime (CSQLAvatar.cpp:625-626).
-- Sans persistance, le joueur perd au logout un bonus paye dont le timer, lui, continue d'exister.
--
-- L'ECART FENRIR
-- VisibleState / SpecialState : les colonnes EXISTENT deja sur game.Characters (Tables/game/Characters.sql:188
-- et :190, DEFAULT 1 / 0) mais ne sont QUE lues, par usp_Character_GetAccountRoster. Aucun UPDATE nulle part,
-- et elles ne sont pas non plus relues a l'entree en monde : PlayerRuntimeState repart du defaut de code.
-- Les commandes GM de Fenrir mutent bien l'etat chaud (GmBasicCommandService -> TribeProgressZoneCommand ->
-- Zone.EconomyMirrors) mais rien ne redescend en base : /hide etait annule par le premier changement de zone.
-- UseOrnament : AUCUNE colonne, aucun TVP, aucun DTO -- le bonus etait perdu au logout.
--
-- CE QUE FAIT CE SCRIPT
-- 1. ajoute game.Characters.UseOrnament (BIT, DEFAULT 0 : le legacy n'y stocke que 0 ou 1) ;
-- 2. ajoute trois colonnes en QUEUE de game.tvp_CharacterProgress et les clauses SET correspondantes aux deux
--    procedures de write-behind, donc AU CHEMIN D'ECRITURE DEJA EN PLACE -- flush periodique
--    (usp_Character_PersistProgressBatch), deconnexion et changement de zone (usp_Character_PersistFinalFlush,
--    seul appelant : PositionWriteBehindHost.FlushCharacterNowAsync). Pas de second chemin d'ecriture : deux
--    chemins sur la meme entite finissent par diverger ;
-- 3. ajoute les trois colonnes en QUEUE de la projection de usp_Character_GetForWorldEntry, le chemin de
--    LECTURE. Un champ persiste mais jamais relu est pire qu'un champ non persiste : il donne l'illusion de
--    fonctionner. Les deux sens sont cables ici.
--
-- POURQUOI LE TVP PORTE DES INT LA OU LES COLONNES SONT TINYINT/BIT
-- Meme posture que RebirthCount/M15PetLuckyBoxPity/Eat*Potion et que les cinq Mount* de Migrations/002 : le
-- TVP est une forme transitoire cote client, pas une ligne stockee, et SQL Server convertit vers la colonne
-- plus etroite a l'ecriture (INT -> BIT : tout non-zero devient 1, 0 reste 0 ; c'est exactement la semantique
-- voulue pour un drapeau que le legacy n'ecrit qu'a 0 ou 1). SpecialState reste large a dessein : le legacy y
-- stocke aussi 2 (Server/ts25zone/S04_MyWork04.cpp:1431-1432 et :1460-1461, GM sur une cible tierce), donc un
-- bool serait faux.
--
-- POURQUOI UN NOUVEAU SCRIPT PLUTOT QU'UNE EDITION DES FICHIERS DE BASE
-- Tables/game/Characters.sql, Schemas/Types/game/tvp_CharacterProgress.sql,
-- StoredProcedures/game/usp_Character_PersistProgressBatch.sql, usp_Character_PersistFinalFlush.sql et
-- usp_Character_GetForWorldEntry.sql sont deja listes dans _manifest.txt, donc journalises par SHA-256 par
-- Fenrir.Tools.DbMigrator sur toute base qui les a appliques une fois (le volume de dev AppHost est
-- persistant). Le migrateur refuse de re-appliquer un chemin journalise dont le contenu a change : une edition
-- en place ferait echouer durement la prochaine execution. Une base FRAICHE applique donc les scripts de base
-- puis cette migration ; une base PERSISTANTE saute les scripts deja journalises et n'applique que celle-ci.
-- Les deux convergent sur la meme forme finale. Ce script se pose APRES Migrations/002, dont il reprend la
-- forme du type a 30 colonnes et y ajoute les trois siennes.
--
-- POURQUOI ALTER ADD POUR LA COLONNE ET DROP+CREATE POUR LE TYPE
-- game.Characters porte de vraies donnees joueur : un ALTER additif avec DEFAULT sur une colonne NOT NULL est
-- une operation de metadonnees seule sur SQL Server (Microsoft Learn, "Add Columns to a Table"), pas de
-- reecriture de pages, lignes existantes preservees a 0 -- valeur sure : 0 = ornement inactif, exactement
-- l'etat qu'avaient de fait tous les personnages avant ce script. Un TABLE type, lui, ne peut pas etre ALTERe
-- pour gagner des colonnes et ne peut pas etre droppe tant qu'une procedure le prend en parametre ; les deux
-- procedures de write-behind sont donc droppees d'abord, comme en Migrations/002.
--
-- CE QUE CE SCRIPT NE FAIT PAS
-- Il ne touche pas usp_Character_GetAccountRoster (elle projette deja VisibleState/SpecialState et ne lit pas
-- UseOrnament : le roster de compte legacy n'expose que les deux premiers, Server/ts25login/S05_MyTransfer.cpp
-- :137-138). Il ne touche pas usp_Character_GetForWorldEntrySummary : son prefixe stable de 19 colonnes est
-- inchange. Il n'ajoute PAS aGoldTime/aSilverTime -- les deux compteurs d'ornement n'existent nulle part cote
-- Fenrir (ni colonne, ni PlayerRuntimeState) ; les persister sans le systeme qui les decremente n'aurait rien
-- reconstruit, c'est un lot a part entiere.
--
-- Les permissions EXECUTE sont inchangees : Schemas/002_roles.sql accorde EXECUTE au niveau SCHEMA
-- (GRANT EXECUTE ON SCHEMA::game TO fenrir_game_role), permission couvrante heritee par tout objet cree dans
-- le schema, maintenant et a l'avenir. Aucune des trois procedures ne porte de grant au niveau objet.

-- 1. La colonne manquante. Garde d'idempotence : le migrateur journalise, mais ce script doit rester rejouable
--    a la main sur une base a l'etat inconnu.
IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'UseOrnament')
ALTER TABLE game.Characters
    ADD UseOrnament BIT NOT NULL
        CONSTRAINT DF_Characters_UseOrnament DEFAULT 0; -- aUseOrnament (Server/Header/CSQLAvatar.cpp:624) : interrupteur du bonus d'ornement plaque or / plaque argent, bascule par TRIBE_WORK sort 9/10 (Server/ts25zone/S04_MyWork02.cpp:11046,11053) et remis a 0 par l'expiration des compteurs (Server/ts25zone/S07_MyGame04.cpp:1290-1293)
GO

-- 2. Dropper les deux procedures qui referencent le type (elles bloquent DROP TYPE), puis le type lui-meme.
DROP PROCEDURE IF EXISTS game.usp_Character_PersistFinalFlush;
DROP PROCEDURE IF EXISTS game.usp_Character_PersistProgressBatch;
DROP TYPE IF EXISTS game.tvp_CharacterProgress;
GO

-- 3. Recreer le type : forme de Migrations/002 (30 colonnes) + les trois nouvelles en queue.
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
    UseOrnament        INT      NOT NULL
);
GO

-- 4. Recreer usp_Character_PersistProgressBatch avec les trois clauses SET ajoutees avant les ecritures
--    FlushSequence/UpdatedAtUtc. Garde d'idempotence (FlushSequence strictement superieure) inchangee.
-- Money/BigMoney/WarPoint restent volontairement absents : les soldes ne bougent que par leurs procedures
-- atomiques dediees, pour qu'un flush last-write-wins ne puisse jamais ecraser un solde. Les trois nouvelles
-- colonnes sont dans la meme categorie que DropItemTime/Eat*Potion/Mount* : etat mono-proprietaire de
-- PlayerRuntimeState, jamais mute par une autre procedure.
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
        c.FlushSequence      = s.FlushSequence,
        c.UpdatedAtUtc       = SYSUTCDATETIME()
    FROM game.Characters AS c
             JOIN @Progress AS s ON s.CharacterId = c.CharacterId
    WHERE s.FlushSequence > c.FlushSequence; -- idempotence guard
END;
GO

-- 5. Recreer usp_Character_PersistFinalFlush avec les trois memes clauses SET. Progression + position dans un
--    seul UPDATE, pour que les deux moities d'un instantane de logout ne puissent pas etre separees par un
--    echec en cours de sequence. C'est aussi le chemin du CHANGEMENT DE ZONE : une transition de zone est une
--    reconnexion complete cote client (ZoneMoveService), donc ces trois colonnes sont le seul porteur de
--    l'etat entre les deux zones -- exactement le role de la copie persistee dans
--    Server/ts25zone/S04_MyWork02.cpp:970-971.
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

-- 6. Le chemin de LECTURE. RS0 est append-only en queue (regle posee par l'en-tete du fichier de base) :
--    VisibleState/SpecialState/UseOrnament se posent apres M15PetLuckyBoxPity, jamais au milieu.
--    CREATE OR ALTER, meme posture que StoredProcedures/game/usp_Character_CreateWithStarterKit_MountFix.sql.
--    Le prefixe stable de 19 colonnes partage avec usp_Character_GetForWorldEntrySummary est intact.
--    Les quatre autres result sets sont repris verbatim.
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
           c.UseOrnament
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
