-- Lot 9 -- quatre compteurs de charge/temps mutes en jeu, persistes par le legacy, perdus par Fenrir --
-- ImproveItemValue / AddItemValue / HighItemValue / TaiyanKeyTimer.
--
-- CE QUE PERSISTE LE LEGACY, ET OU
--   ImproveItemValue (aImproveItemValue / alias aSweetPotaito)  STRUCT.h:453  CSQLAvatar.cpp:668
--                     credite par parchemin (Server/ts25zone/S04_MyWork03.cpp:2440-2441), consomme -1 a
--                     l'enchant avec broadcast S146SWEET_POTATO (Server/ts25zone/S04_MyWork02.cpp:2908-2912,
--                     :3156-3160).
--   AddItemValue      (aAddItemValue)  STRUCT.h:402  CSQLAvatar.cpp:669
--                     credite par parchemin, plafond MAX_NUMBER_SIZE (Server/ts25zone/S04_MyWork03.cpp:2808-2813),
--                     consomme -1 sur la combinaison, broadcast S028LUCKY_COMBINE (S04_MyWork02.cpp:3722-3726).
--   HighItemValue     (aHighItemValue)  STRUCT.h:403  CSQLAvatar.cpp:670
--                     credite par parchemin (S04_MyWork03.cpp:2831-2832), entree de GetHighLowItemProbability
--                     (S04_MyWork02.cpp:4008,4159), consomme -1 avec broadcast S029LUCKY_UPGRADE
--                     (S04_MyWork02.cpp:4027-4031,4178-4182,14397-14401).
--   TaiyanKeyTimer    (aZone125Time)  STRUCT.h:385  CSQLAvatar.cpp:607
--                     credite par la cle Taiyan, objet 1049 (S04_MyWork03.cpp:2392-2402), decremente d'une
--                     minute avec Quit() a l'epuisement et broadcast S021ZONE_125_TIME
--                     (Server/ts25zone/S07_MyGame04.cpp:1067-1079). C'est le sous-code 21 deja emis par
--                     Fenrir (TimedBuffCountdownSystem.cs:84-87), sous PlayerRuntimeState.TaiyanKeyTimer.
-- Les quatre colonnes sont FIELD_AVATAR0 dans CreateAvatarColumn SANS #ifdef -- elles comptent dans les deux
-- configurations livrees -- donc persistees par le meme UPDATE d'avatar que le reste
-- (Server/ts25playuser/S08_MyDB.cpp:99) et relues au login (Server/ts25login/S08_MyDB.cpp:571).
--
-- L'ECART FENRIR : les quatre sont deja mutees (Zone.EconomyMirrors.cs -> TribeProgressZoneCommand pour les
-- trois premieres ; TimedBuffCountdownSystem.cs pour la quatrieme) mais n'atteignaient aucune colonne, aucun
-- TVP, aucun DTO -- perdues a la deconnexion ET au changement de zone.
--
-- ANGLE MORT DECOUVERT EN PREPARANT CE LOT -- A CORRIGER DANS LE MEME SCRIPT, PAS A COTE
-- game.tvp_CharacterProgress ne peut pas etre ALTERe (type TABLE) : chaque lot le DROP+RECREE en entier, et
-- Migrations/010_character_autobuff_and_rankbuff_writeback.sql puis
-- Migrations/011_avatar_counters_and_bottles_writeback.sql -- tous deux ecrits en parallele de
-- Migrations/008_autohunt_buffx2_premium_pet_writeback.sql -- se sont chacun rebases sur la forme laissee par
-- Migrations/007 (37/42 colonnes), PAS sur celle de 008 (48 colonnes). L'AVERTISSEMENT DE SEQUENCAGE ecrit en
-- toutes lettres dans l'en-tete de 008 ("le dernier script applique gagne et efface silencieusement les
-- colonnes des autres") s'est donc realise : 011, dernier des Migrations/ dans l'ordre du manifeste avant ce
-- lot, recree le type et les deux procedures SANS les six colonnes de 008 (AutoTime, AutoTime2, BuffX2Time,
-- PremiumExpireUtc, PetGrowth, PetActivity). Migrations/012_autohunt_premium_pet_writeback_restore.sql a
-- depuis restaure ces six colonnes au type et aux clauses SET des deux procedures, mais SANS le parametre
-- @Costumes / la transaction / le remplacement de penderie que Migrations/008 avait aussi poses -- alors que
-- Fenrir.Data.Characters.CharacterRepository.PersistProgressAsync/PersistFinalFlushAsync passent deja un
-- parametre @Costumes des que la penderie n'est pas vide (CharacterRepository.cs). Ce script restaure donc
-- @Costumes/la transaction/le remplacement de penderie EN PLUS d'ajouter ses quatre colonnes -- un type SQL
-- sans @Costumes face a un appelant qui le fournit echoue durement des qu'un personnage porte un costume.
--
-- POURQUOI UN NOUVEAU SCRIPT PLUTOT QU'UNE EDITION DES FICHIERS DE BASE OU DES MIGRATIONS 008/010/011/012
-- Les migrations et les fichiers de base sont deja listes dans _manifest.txt, donc journalises par SHA-256
-- par Fenrir.Tools.DbMigrator sur toute base qui les a appliques une fois -- le migrateur refuse de
-- re-appliquer un chemin journalise dont le contenu a change. Une base FRAICHE applique la chaine complete du
-- manifeste puis ce script ; une base PERSISTANTE saute les scripts deja journalises et n'applique que
-- celui-ci. Les deux convergent sur la meme forme terminale.
--
-- CE QUE CE SCRIPT NE FAIT PAS
-- Il ne touche pas usp_Character_GetForWorldEntrySummary (prefixe stable de 19 colonnes, inchange). Il ne
-- deplace ni ne renomme aucune colonne existante -- uniquement des ajouts en queue, ordre exact du record
-- CharacterProgressTvp et de CharacterWorldSnapshotDto au moment de l'ecriture (avant les colonnes d'un lot
-- suivant qui s'enregistrerait apres celui-ci -- voir l'avertissement de sequencage plus haut : la migration
-- qui s'enregistre EN DERNIER doit reprendre l'union complete, ce script n'est pas garanti d'etre celle-la).
-- Il n'ajoute AUCUN IHostedService : ces quatre champs sont mono-proprietaires de PlayerRuntimeState (meme
-- categorie que DropItemTime/Eat*Potion), donc portes par les DEUX chemins de write-behind deja enregistres
-- (ProgressWriteBehindHost pour le flush periodique, PositionWriteBehindHost.FlushCharacterNowAsync pour la
-- deconnexion et le changement de zone), jamais un troisieme chemin d'ecriture.

-- ---------------------------------------------------------------------------
-- 1. game.Characters : quatre nouvelles colonnes. Valeur sure pour les lignes existantes : 0 partout (aucune
--    charge/aucun temps restant, l'etat d'un avatar qui n'a jamais consomme le parchemin/la cle correspondante).
-- ---------------------------------------------------------------------------
IF COL_LENGTH('game.Characters', 'ImproveItemValue') IS NULL
ALTER TABLE game.Characters
    ADD ImproveItemValue INT NOT NULL
            CONSTRAINT DF_Characters_ImproveItemValue DEFAULT 0
            CONSTRAINT CK_Characters_ImproveItemValue CHECK (ImproveItemValue >= 0),
        AddItemValue INT NOT NULL
            CONSTRAINT DF_Characters_AddItemValue DEFAULT 0
            CONSTRAINT CK_Characters_AddItemValue CHECK (AddItemValue >= 0),
        HighItemValue INT NOT NULL
            CONSTRAINT DF_Characters_HighItemValue DEFAULT 0
            CONSTRAINT CK_Characters_HighItemValue CHECK (HighItemValue >= 0),
        TaiyanKeyTimer INT NOT NULL
            CONSTRAINT DF_Characters_TaiyanKeyTimer DEFAULT 0
            CONSTRAINT CK_Characters_TaiyanKeyTimer CHECK (TaiyanKeyTimer >= 0);
GO

-- ---------------------------------------------------------------------------
-- 2. Les deux procedures qui referencent le type bloquent son DROP : les supprimer d'abord, puis le type.
-- ---------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS game.usp_Character_PersistFinalFlush;
DROP PROCEDURE IF EXISTS game.usp_Character_PersistProgressBatch;
DROP TYPE IF EXISTS game.tvp_CharacterProgress;
GO

-- ---------------------------------------------------------------------------
-- 3. Le type : les 59 colonnes restaurees par Migrations/012 (53 de 011 + les 6 oubliees de 008), plus les
--    quatre nouvelles de ce lot en queue. PremiumExpireUtc reste BIGINT (time_t Unix, USE_PREMIUM_LONGTIME) ;
--    les quatre nouvelles restent INT, meme categorie que DropItemTime/ProtectForHalo -- des compteurs,
--    jamais un solde partage.
-- ---------------------------------------------------------------------------
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
    RankBuffType             INT          NOT NULL,
    AutoTime                 INT          NOT NULL,
    AutoTime2                INT          NOT NULL,
    BuffX2Time               INT          NOT NULL,
    PremiumExpireUtc         BIGINT       NOT NULL,
    PetGrowth                INT          NOT NULL,
    PetActivity              INT          NOT NULL,
    ImproveItemValue         INT          NOT NULL,
    AddItemValue             INT          NOT NULL,
    HighItemValue            INT          NOT NULL,
    TaiyanKeyTimer           INT          NOT NULL
);
GO

-- ---------------------------------------------------------------------------
-- 4. Le flush periodique. @Costumes/la transaction/le remplacement de penderie borne par @Applied sont repris
--    de Migrations/008 (jamais restaures par 010/011/012) ; CharacterRepository.PersistProgressAsync les
--    fournit deja. Garde d'idempotence (FlushSequence strictement croissante) inchangee.
-- ---------------------------------------------------------------------------
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
        c.AutoTime                 = s.AutoTime,
        c.AutoTime2                = s.AutoTime2,
        c.BuffX2Time               = s.BuffX2Time,
        c.PremiumExpireUtc         = s.PremiumExpireUtc,
        c.PetGrowth                = s.PetGrowth,
        c.PetActivity              = s.PetActivity,
        c.ImproveItemValue         = s.ImproveItemValue,
        c.AddItemValue             = s.AddItemValue,
        c.HighItemValue            = s.HighItemValue,
        c.TaiyanKeyTimer           = s.TaiyanKeyTimer,
        c.FlushSequence            = s.FlushSequence,
        c.UpdatedAtUtc             = SYSUTCDATETIME()
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

-- ---------------------------------------------------------------------------
-- 5. Le flush terminal -- deconnexion (GameConnectionHost) et changement de zone (ZoneMoveService), tous deux
--    via PositionWriteBehindHost.FlushCharacterNowAsync. Progression + position + penderie dans la meme
--    transaction : ce chemin n'a pas de cycle suivant pour rattraper une moitie perdue.
-- ---------------------------------------------------------------------------
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
        c.AutoTime                 = p.AutoTime,
        c.AutoTime2                = p.AutoTime2,
        c.BuffX2Time               = p.BuffX2Time,
        c.PremiumExpireUtc         = p.PremiumExpireUtc,
        c.PetGrowth                = p.PetGrowth,
        c.PetActivity              = p.PetActivity,
        c.ImproveItemValue         = p.ImproveItemValue,
        c.AddItemValue             = p.AddItemValue,
        c.HighItemValue            = p.HighItemValue,
        c.TaiyanKeyTimer           = p.TaiyanKeyTimer,
        c.MapId                    = q.MapId,
        c.PosX                     = q.PosX,
        c.PosY                     = q.PosY,
        c.PosZ                     = q.PosZ,
        c.Heading                  = q.Heading,
        c.FlushSequence            = q.FlushSequence,
        c.UpdatedAtUtc             = SYSUTCDATETIME()
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
-- 6. Le chemin de LECTURE. RS0 est append-only en queue, mappe par ORDINAL sur CharacterWorldSnapshotDto :
--    AutoTime/BuffX2Time (deja lus cote C#, jamais projetes cote SQL depuis 011) puis les quatre nouvelles
--    colonnes de ce lot se posent tout en fin, jamais au milieu. Le prefixe stable de 19 colonnes partage
--    avec usp_Character_GetForWorldEntrySummary est intact ; les quatre autres result sets sont repris
--    verbatim.
-- ---------------------------------------------------------------------------
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
           c.RankBuffType,
           c.AutoTime,
           c.BuffX2Time,
           c.ImproveItemValue,
           c.AddItemValue,
           c.HighItemValue,
           c.TaiyanKeyTimer
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
