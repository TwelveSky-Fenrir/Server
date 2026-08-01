-- Lot 3 -- six compteurs d'avatar mutes en jeu, persistes par le legacy, et perdus a la deconnexion cote
-- Fenrir.
--
-- Les six sont des colonnes d'avatar a part entiere cote legacy : CreateAvatarColumn
-- (Server/Header/CSQLAvatar.cpp:556-756) est l'unique fonction qui construit la liste de colonnes pour
-- INSERT/SELECT/UPDATE, et elle les declare toutes SANS #ifdef. Le UPDATE part du write-behind de
-- ts25playuser (Server/ts25playuser/S08_MyDB.cpp:99, case 2 de MakeQueryForSave) sur l'AVATAR_INFO en
-- memoire partagee -- wAvatar (Server/Header/Protocol/DEFINE.h:715) -- donc toute ecriture faite par la
-- zone est durable.
--
--   ProtectForHalo            aProtectForHalo,    Server/Header/CSQLAvatar.cpp:595
--                             stock cash consomme a l'echec d'enchantement de halo
--                             (Server/ts25zone/S04_MyWork02.cpp:10927-10931 : wAvatar.aProtectForHalo--),
--                             credite par objet cash (Server/ts25zone/S04_MyWork03.cpp:3414-3415).
--   BonusItemLevel            aBonusItemLevel,    Server/Header/CSQLAvatar.cpp:621
--                             palier de niveau arme mais non reclame, pose en
--                             Server/ts25zone/S07_MyGame03.cpp:4787 (branche #else compilee, LNW33 defini),
--                             remis a 0 a la reclamation (Server/ts25zone/S04_MyWork02.cpp:11036).
--   BonusItemValue            aBonusItemValue,    Server/Header/CSQLAvatar.cpp:620
--                             le drapeau du meme couple : TRUE en S07_MyGame03.cpp:4786, FALSE en
--                             S04_MyWork02.cpp:11035, diffuses ensemble par sort 107. Jamais l'un sans
--                             l'autre.
--   TribeNotifyScrollCount    aTribeNotifyNum,    Server/Header/CSQLAvatar.cpp:648
--                             stock de charges achete (objet 566, Server/ts25zone/S04_MyWork03.cpp:1488-1490),
--                             refus si < 1 puis decrement (Server/ts25zone/S04_MyWork02.cpp:13798-13808).
--   TribeFourReturnAllowance  aReturnTribeNum,    Server/Header/CSQLAvatar.cpp:639
--                             quota de RETOUR depuis la tribu 4 : refus si < 1
--                             (Server/ts25zone/S04_MyWork02.cpp:7509), decrement seulement dans la branche
--                             retour (S04_MyWork02.cpp:7558), credit par l'objet 1189
--                             (Server/ts25zone/S04_MyWork03.cpp:4102-4104).
--   BottleSlots / DrunkBottleIndex
--                             aBottle[10]/aBottleCount[10] (Server/Header/Protocol/STRUCT.h:527-528,
--                             MAX_AVATAR_BOTTLE_NUM = 10 en Server/Header/Protocol/DEFINE.h:392) et
--                             aBottleIndex (Server/Header/CSQLAvatar.cpp:677).
--
-- POURQUOI TribeFourReturnAllowance N'EST PAS game.Characters.TribeTransferPermitCount
-- La colonne existante porte le stock de parchemins de transfert de faction (world.Items 8153/8154), un
-- mecanisme different et deja implemente par ailleurs (game.usp_Character_ApplyTribeScrollConversion,
-- src/Fenrir.Application.Game/Domain/Inventory/UseItems/TribeScrollTransferUseItemHandler.cs). Le quota
-- legacy aReturnTribeNum vient de l'objet 1189 et ne gouverne que la branche retour de la conversion tribu
-- 4. Deux compteurs distincts cote legacy, deux colonnes distinctes ici : les fondre casserait le jour ou
-- l'objet 1189 sera porte. TribeTransferPermitCount reste inchangee.
--
-- COMMENT BottleSlots EST ENCODE
-- Le legacy serialise deja les 10 paires en chaines de largeur fixe (SetAvatar,
-- Server/Header/CSQLAvatar.cpp:308-313) : 5 caracteres par aBottle[i] avec le clamp EFIX_ITEM (0 si < 2 ou
-- > 99999, Server/Header/CSQLAvatar.cpp:24-27) et 2 caracteres par aBottleCount[i], vers char aBottle[51] /
-- char aBottleCount[21] (Server/Header/CSQLDatabase.h:128-129), enregistres en colonnes par FIELD_AVATAR1
-- (Server/Header/CSQLAvatar.cpp:675-676). Fenrir garde les memes largeurs de champ mais UNE seule colonne
-- de 70 caracteres (10 groupes de 5+2) : les deux moities d'un slot ne peuvent alors pas etre ecrites
-- l'une sans l'autre, ce qui est exactement l'invariant que suppose l'assainissement d'entree en monde
-- (Server/ts25zone/S04_MyWork02.cpp:920-927 : si l'un des deux est < 1, les deux passent a 0). N'' pour les
-- lignes existantes = 10 slots vides, la valeur par defaut deja utilisee en memoire.
--
-- CE QUI RESTE VOLONTAIREMENT EPHEMERE
-- aBottleTime (Server/Header/Protocol/STRUCT.h:530), le chrono d'ivresse, est ABSENT de CreateAvatarColumn
-- et remis a 0 a chaque entree en monde (Server/ts25zone/S04_MyWork02.cpp:919).
-- PlayerRuntimeState.DrunkBottleTicksRemaining n'a donc aucune colonne ici et ne doit pas en avoir.
--
-- POURQUOI UN NOUVEAU SCRIPT PLUTOT QU'UNE EDITION DES FICHIERS DE BASE
-- Tables/game/Characters.sql, Schemas/Types/game/tvp_CharacterProgress.sql, les deux procedures de
-- write-behind et usp_Character_GetForWorldEntry.sql sont deja listes dans _manifest.txt, donc journalises
-- par SHA-256 par Fenrir.Tools.DbMigrator sur toute base qui les a appliques une fois. Le migrateur refuse
-- de re-appliquer un chemin journalise dont le contenu a change. Meme raisonnement que Migrations/002.
--
-- POURQUOI DROP+RECREATE DU TYPE, ET CE QUE CE SCRIPT REPREND DES LOTS VOISINS
-- Un type TABLE ne s'ALTER pas et ne se DROP pas tant qu'une procedure le prend en parametre. Toute
-- extension de game.tvp_CharacterProgress est donc forcement une recreation integrale, et le mapper TVP
-- genere par CaeriusNet lie POSITIONNELLEMENT : l'ordre des colonnes du type doit etre le miroir exact de
-- l'ordre des parametres de Fenrir.Data.Abstractions.Characters.CharacterProgressTvp, et l'ordre de RS0
-- celui de CharacterWorldSnapshotDto. Ce script recree donc le type comme l'UNION de tout ce qui existe au
-- moment de son ecriture, dans l'ordre exact du record C# : 30 colonnes de Migrations/002, puis
-- VisibleState/SpecialState/UseOrnament (Migrations/006), Title/Halo/TeacherPoint/WarPointDelta/
-- BloodCoinDelta (lot cosmetiques-et-monnaies), PetExpX2Time/AnimalAbsorbTime/AnimalAbsorbState/
-- CostumeIndex (Migrations/007), les sept du present lot, enfin AutoBuffTime/AutoBuffSkill/RankPointDate/
-- RankBuffType (Migrations/010). Il doit donc s'appliquer APRES tous ceux-la, ce que garantit sa position
-- en fin de bloc Migrations/ du manifeste. Si un lot parallele ajoute encore des colonnes au record C#, le
-- script qui s'enregistrera en dernier devra a son tour declarer l'union complete.

-- 1. Les sept colonnes de destination sur game.Characters. Valeurs par defaut sures pour les lignes
--    existantes : 0 partout, N'' pour les bouteilles (10 slots vides), -1 pour l'index de bouteille bue
--    (meme convention "index ou -1" que MountSlotIndex). Chaque ADD est garde par sys.columns : le script
--    reste re-executable a la main sur une base a l'etat inconnu.
IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'ProtectForHalo')
ALTER TABLE game.Characters
    ADD ProtectForHalo INT NOT NULL
        CONSTRAINT DF_Characters_ProtectForHalo DEFAULT 0
        CONSTRAINT CK_Characters_ProtectForHalo CHECK (ProtectForHalo >= 0);
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'BonusItemLevel')
ALTER TABLE game.Characters
    ADD BonusItemLevel INT NOT NULL
        CONSTRAINT DF_Characters_BonusItemLevel DEFAULT 0
        CONSTRAINT CK_Characters_BonusItemLevel CHECK (BonusItemLevel >= 0);
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'BonusItemValue')
ALTER TABLE game.Characters
    ADD BonusItemValue BIT NOT NULL
        CONSTRAINT DF_Characters_BonusItemValue DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'TribeNotifyScrollCount')
ALTER TABLE game.Characters
    ADD TribeNotifyScrollCount INT NOT NULL
        CONSTRAINT DF_Characters_TribeNotifyScrollCount DEFAULT 0
        CONSTRAINT CK_Characters_TribeNotifyScrollCount CHECK (TribeNotifyScrollCount >= 0);
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'TribeFourReturnAllowance')
ALTER TABLE game.Characters
    ADD TribeFourReturnAllowance INT NOT NULL
        CONSTRAINT DF_Characters_TribeFourReturnAllowance DEFAULT 0
        CONSTRAINT CK_Characters_TribeFourReturnAllowance CHECK (TribeFourReturnAllowance >= 0);
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'BottleSlots')
ALTER TABLE game.Characters
    ADD BottleSlots NVARCHAR(70) NOT NULL
        CONSTRAINT DF_Characters_BottleSlots DEFAULT N''
        CONSTRAINT CK_Characters_BottleSlots CHECK (LEN(BottleSlots) IN (0, 70));
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'DrunkBottleIndex')
ALTER TABLE game.Characters
    ADD DrunkBottleIndex INT NOT NULL
        CONSTRAINT DF_Characters_DrunkBottleIndex DEFAULT -1
        CONSTRAINT CK_Characters_DrunkBottleIndex CHECK (DrunkBottleIndex BETWEEN -1 AND 9);
GO

-- 2. Dropper les deux procedures qui referencent le type (elles bloquent DROP TYPE), puis le type lui-meme.
DROP PROCEDURE IF EXISTS game.usp_Character_PersistFinalFlush;
DROP PROCEDURE IF EXISTS game.usp_Character_PersistProgressBatch;
DROP TYPE IF EXISTS game.tvp_CharacterProgress;
GO

-- 3. Recreer le type, miroir exact de CharacterProgressTvp. Les sept du present lot sont dans la meme
-- categorie que DropItemTime/Eat*Potion/Mount*/VisibleState : etat mono-proprietaire porte par
-- PlayerRuntimeState, re-encode a chaque flush, sans autre ecrivain. Aucun solde concurrent parmi eux --
-- Money/BigMoney restent dehors, WarPoint/BloodCoin ne passent ici qu'en DELTA (voir les clauses SET).
CREATE TYPE game.tvp_CharacterProgress AS TABLE
(
    CharacterId              INT          NOT NULL,
    FlushSequence            BIGINT       NOT NULL,
    Level                    SMALLINT     NOT NULL,
    Level2                   SMALLINT     NOT NULL,
    Experience               BIGINT       NOT NULL,
    Life                     INT          NOT NULL,
    MaxLife                  INT          NOT NULL,
    Mana                     INT          NOT NULL,
    MaxMana                  INT          NOT NULL,
    StatVit                  INT          NOT NULL,
    StatStr                  INT          NOT NULL,
    StatInt                  INT          NOT NULL,
    StatDex                  INT          NOT NULL,
    StatPoints               INT          NOT NULL,
    SkillPoints              INT          NOT NULL,
    ContributionPoints       INT          NOT NULL,
    Exp2                     INT          NOT NULL,
    RebirthCount             INT          NOT NULL,
    EatLifePotion            INT          NOT NULL,
    EatManaPotion            INT          NOT NULL,
    EatStrPotion             INT          NOT NULL,
    EatDexPotion             INT          NOT NULL,
    EatElePotion             INT          NOT NULL,
    DropItemTime             INT          NOT NULL,
    M15PetLuckyBoxPity       INT          NOT NULL,
    MountItemId              INT          NOT NULL,
    MountExpActivity         INT          NOT NULL,
    MountPower               INT          NOT NULL,
    MountSlotIndex           INT          NOT NULL,
    MountTime                INT          NOT NULL,
    VisibleState             INT          NOT NULL,
    SpecialState             INT          NOT NULL,
    UseOrnament              INT          NOT NULL,
    Title                    INT          NOT NULL,
    Halo                     INT          NOT NULL,
    TeacherPoint             INT          NOT NULL,
    WarPointDelta            INT          NOT NULL,
    BloodCoinDelta           INT          NOT NULL,
    PetExpX2Time             INT          NOT NULL,
    AnimalAbsorbTime         INT          NOT NULL,
    AnimalAbsorbState        INT          NOT NULL,
    CostumeIndex             INT          NOT NULL,
    ProtectForHalo           INT          NOT NULL,
    BonusItemLevel           INT          NOT NULL,
    BonusItemValue           BIT          NOT NULL,
    TribeNotifyScrollCount   INT          NOT NULL,
    TribeFourReturnAllowance INT          NOT NULL,
    BottleSlots              NVARCHAR(70) NOT NULL,
    DrunkBottleIndex         INT          NOT NULL,
    AutoBuffTime             INT          NOT NULL,
    AutoBuffSkill            NVARCHAR(48) NOT NULL,
    RankPointDate            INT          NOT NULL,
    RankBuffType             INT          NOT NULL
);
GO

-- 4. Recreer usp_Character_PersistProgressBatch. Garde d'idempotence (FlushSequence strictement superieure)
--    inchangee. WarPoint/BloodCoin restent credites RELATIVEMENT pour composer avec leurs procedures
--    atomiques dediees au lieu de les concurrencer.
CREATE PROCEDURE game.usp_Character_PersistProgressBatch @Progress game.tvp_CharacterProgress READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE c
    SET c.Level                    = s.Level,
        c.Level2                   = s.Level2,
        c.Experience               = s.Experience,
        c.Life                     = s.Life,
        c.MaxLife                  = s.MaxLife,
        c.Mana                     = s.Mana,
        c.MaxMana                  = s.MaxMana,
        c.StatVit                  = s.StatVit,
        c.StatStr                  = s.StatStr,
        c.StatInt                  = s.StatInt,
        c.StatDex                  = s.StatDex,
        c.StatPoints               = s.StatPoints,
        c.SkillPoints              = s.SkillPoints,
        c.ContributionPoints       = s.ContributionPoints,
        c.Exp2                     = s.Exp2,
        c.RebirthCount             = s.RebirthCount,
        c.EatLifePotion            = s.EatLifePotion,
        c.EatManaPotion            = s.EatManaPotion,
        c.EatStrPotion             = s.EatStrPotion,
        c.EatDexPotion             = s.EatDexPotion,
        c.EatElePotion             = s.EatElePotion,
        c.DropItemTime             = s.DropItemTime,
        c.M15PetLuckyBoxPity       = s.M15PetLuckyBoxPity,
        c.MountItemId              = s.MountItemId,
        c.MountExpActivity         = s.MountExpActivity,
        c.MountPower               = s.MountPower,
        c.MountSlotIndex           = s.MountSlotIndex,
        c.MountTime                = s.MountTime,
        c.VisibleState             = s.VisibleState,
        c.SpecialState             = s.SpecialState,
        c.UseOrnament              = s.UseOrnament,
        c.Title                    = s.Title,
        c.Halo                     = s.Halo,
        c.TeacherPoint             = s.TeacherPoint,
        c.WarPoint                 = c.WarPoint + s.WarPointDelta,
        c.BloodCoin                = c.BloodCoin + s.BloodCoinDelta,
        c.PetExpX2Time             = s.PetExpX2Time,
        c.AnimalAbsorbTime         = s.AnimalAbsorbTime,
        c.AnimalAbsorbState        = s.AnimalAbsorbState,
        c.CostumeIndex             = s.CostumeIndex,
        c.ProtectForHalo           = s.ProtectForHalo,
        c.BonusItemLevel           = s.BonusItemLevel,
        c.BonusItemValue           = s.BonusItemValue,
        c.TribeNotifyScrollCount   = s.TribeNotifyScrollCount,
        c.TribeFourReturnAllowance = s.TribeFourReturnAllowance,
        c.BottleSlots              = s.BottleSlots,
        c.DrunkBottleIndex         = s.DrunkBottleIndex,
        c.AutoBuffTime             = s.AutoBuffTime,
        c.AutoBuffSkill            = s.AutoBuffSkill,
        c.RankPointDate            = s.RankPointDate,
        c.RankBuffType             = s.RankBuffType,
        c.FlushSequence            = s.FlushSequence,
        c.UpdatedAtUtc             = SYSUTCDATETIME()
    FROM game.Characters AS c
             JOIN @Progress AS s ON s.CharacterId = c.CharacterId
    WHERE s.FlushSequence > c.FlushSequence; -- idempotence guard
END;
GO

-- 5. Recreer usp_Character_PersistFinalFlush. C'est le chemin de la DECONNEXION (GameConnectionHost) ET du
--    CHANGEMENT DE ZONE (ZoneMoveService), tous deux via PositionWriteBehindHost.FlushCharacterNowAsync :
--    progression et position dans un seul UPDATE, pour qu'une panne en cours de sequence ne puisse pas
--    couper la photo de deconnexion en deux.
CREATE PROCEDURE game.usp_Character_PersistFinalFlush @Progress game.tvp_CharacterProgress READONLY,
                                                      @Position game.tvp_CharacterPosition READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE c
    SET c.Level                    = p.Level,
        c.Level2                   = p.Level2,
        c.Experience               = p.Experience,
        c.Life                     = p.Life,
        c.MaxLife                  = p.MaxLife,
        c.Mana                     = p.Mana,
        c.MaxMana                  = p.MaxMana,
        c.StatVit                  = p.StatVit,
        c.StatStr                  = p.StatStr,
        c.StatInt                  = p.StatInt,
        c.StatDex                  = p.StatDex,
        c.StatPoints               = p.StatPoints,
        c.SkillPoints              = p.SkillPoints,
        c.ContributionPoints       = p.ContributionPoints,
        c.Exp2                     = p.Exp2,
        c.RebirthCount             = p.RebirthCount,
        c.EatLifePotion            = p.EatLifePotion,
        c.EatManaPotion            = p.EatManaPotion,
        c.EatStrPotion             = p.EatStrPotion,
        c.EatDexPotion             = p.EatDexPotion,
        c.EatElePotion             = p.EatElePotion,
        c.DropItemTime             = p.DropItemTime,
        c.M15PetLuckyBoxPity       = p.M15PetLuckyBoxPity,
        c.MountItemId              = p.MountItemId,
        c.MountExpActivity         = p.MountExpActivity,
        c.MountPower               = p.MountPower,
        c.MountSlotIndex           = p.MountSlotIndex,
        c.MountTime                = p.MountTime,
        c.VisibleState             = p.VisibleState,
        c.SpecialState             = p.SpecialState,
        c.UseOrnament              = p.UseOrnament,
        c.Title                    = p.Title,
        c.Halo                     = p.Halo,
        c.TeacherPoint             = p.TeacherPoint,
        c.WarPoint                 = c.WarPoint + p.WarPointDelta,
        c.BloodCoin                = c.BloodCoin + p.BloodCoinDelta,
        c.PetExpX2Time             = p.PetExpX2Time,
        c.AnimalAbsorbTime         = p.AnimalAbsorbTime,
        c.AnimalAbsorbState        = p.AnimalAbsorbState,
        c.CostumeIndex             = p.CostumeIndex,
        c.ProtectForHalo           = p.ProtectForHalo,
        c.BonusItemLevel           = p.BonusItemLevel,
        c.BonusItemValue           = p.BonusItemValue,
        c.TribeNotifyScrollCount   = p.TribeNotifyScrollCount,
        c.TribeFourReturnAllowance = p.TribeFourReturnAllowance,
        c.BottleSlots              = p.BottleSlots,
        c.DrunkBottleIndex         = p.DrunkBottleIndex,
        c.AutoBuffTime             = p.AutoBuffTime,
        c.AutoBuffSkill            = p.AutoBuffSkill,
        c.RankPointDate            = p.RankPointDate,
        c.RankBuffType             = p.RankBuffType,
        c.MapId                    = q.MapId,
        c.PosX                     = q.PosX,
        c.PosY                     = q.PosY,
        c.PosZ                     = q.PosZ,
        c.Heading                  = q.Heading,
        c.FlushSequence            = q.FlushSequence,
        c.UpdatedAtUtc             = SYSUTCDATETIME()
    FROM game.Characters AS c
             JOIN @Progress AS p ON p.CharacterId = c.CharacterId
             JOIN @Position AS q ON q.CharacterId = c.CharacterId
    WHERE q.FlushSequence > c.FlushSequence; -- idempotence guard
END;
GO

-- 6. Le chemin de LECTURE. Sans ces sept colonnes dans RS0, elles seraient ecrites et jamais relues : pire
--    que pas de persistance du tout, parce que le compteur paraitrait fonctionner en session avant de
--    repartir de zero au login suivant.
-- RS0 reste append-only en queue -- CharacterWorldSnapshotDto lit par ORDINAL (mapper genere par
-- CaeriusNet), donc une colonne inseree ailleurs qu'a la fin decalerait silencieusement toute la
-- projection. Les sept nouvelles arrivent apres CostumeIndex, derniere colonne posee par Migrations/007.
-- Le prefixe stable de 19 colonnes est intact : usp_Character_GetForWorldEntrySummary n'est pas touchee.
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
           c.ProtectForHalo,
           c.BonusItemLevel,
           c.BonusItemValue,
           c.TribeNotifyScrollCount,
           c.TribeFourReturnAllowance,
           c.BottleSlots,
           c.DrunkBottleIndex,
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
