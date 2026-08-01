-- Lot 12 -- cinq compteurs mutes en jeu, persistes par le legacy, jamais ecrits cote Fenrir --
-- EliteDungeonTime / DungeonKeyTime / IvyHallTicketTime / ScrollOfSeekersTime / FightingGodForDestroy.
--
-- CE QUE PERSISTE LE LEGACY, ET OU (CreateAvatarColumn, Server/Header/CSQLAvatar.cpp:556-756, hors de tout
-- #ifdef -- compte dans ReleaseM33 et ReleaseEU33 -- liste partagee SELECT et INSERT/UPDATE :769-812)
--   EliteDungeonTime   aZone101Time    STRUCT.h:384  CSQLAvatar.cpp:606
--                      items 1047/1097/1098 (Elite Dungeon Ticket L/M/S) creditent 180/120/60 via
--                      wCheckAdd(aZone101Time, tAddTime) puis += (S04_MyWork03.cpp:2352-2367).
--   DungeonKeyTime     aZone175Time    STRUCT.h:383  CSQLAvatar.cpp:605
--                      item 1048 (God Temple/Labyrinth Key), aLevel1>=100, credite 1 (S04_MyWork03.cpp:2376-2384).
--   IvyHallTicketTime  aZone050Time2   STRUCT.h:480  CSQLAvatar.cpp:660
--                      items 553/1219 (Ivy Hall Ticket S/L) creditent 180/360, plafond 1576800
--                      (S04_MyWork03.cpp:4965-4981). Le champ voisin aZone050Time2 (commente '//aJuWallTime')
--                      est mort (ligne commentee CSQLAvatar.cpp:643) -- jamais une preuve de persistance ici.
--   ScrollOfSeekersTime aZone126Time   STRUCT.h:386  CSQLAvatar.cpp:608
--                      items 1124/1187/7016/8409/8410 creditent 180 ou 900 (S04_MyWork03.cpp:2459-2474).
--   FightingGodForDestroy aFightingGodForDestroy STRUCT.h:379  CSQLAvatar.cpp:600
--                      items 1121/1122/1123/1234, 1<=aLevel1<=112, creditent 60/120/180
--                      (S04_MyWork03.cpp:2275-2294).
-- Ecriture par le write-behind ts25playuser (S07_MyGame01.cpp:1143-1147 -> S08_MyDB.cpp:99, GetAvatar UPDATE).
--
-- L'ECART FENRIR : les cinq sont deja mutes (Zone.EconomyMirrors.cs -> ApplyTribeProgressCommand pour les
-- quatre premiers, avec MarkProgressDirty(Progression) deja pose ligne 711-712 ; TimedBuffCountdownSystem.cs
-- TickGroupA pour FightingGodForDestroy) mais n'atteignaient aucune colonne, aucun TVP, aucun DTO -- perdus a
-- la deconnexion ET au changement de zone. Confirme par grep : aucune occurrence dans src/Fenrir.Data*,
-- Database/ avant ce script.
--
-- PIEGE CONNU, VOLONTAIREMENT NON RESOLU ICI (hors perimetre de ce lot) : PlayerRuntimeState porte, pour
-- EliteDungeonTime/IvyHallTicketTime/ScrollOfSeekersTime, un DEUXIEME champ runtime distinct qui mute le MEME
-- compteur legacy -- respectivement Zone101Time/Zone050Time2/Zone126Time (PlayerRuntimeState.TimedBuffs.cs),
-- decrementes chaque minute par TimedBuffCountdownSystem.TickPaidZones et credites ailleurs (ex.
-- HighLevelExperienceOutcomeApplier pour Zone101Time). Ce script persiste le compteur EliteDungeonTime/
-- IvyHallTicketTime/ScrollOfSeekersTime tel qu'il existe cote C# aujourd'hui (le solde credite par le
-- ticket) ; il ne fusionne PAS avec Zone101Time/Zone050Time2/Zone126Time, qui restent non persistes. Fusionner
-- les deux est un changement de logique de jeu (Domain/World/Zone.EconomyMirrors.cs,
-- Domain/Simulation/TimedBuffCountdownSystem.cs), hors du perimetre d'ecriture de ce lot (Fenrir.Data*/
-- Database/ + sites de sauvegarde). DungeonKeyTime et FightingGodForDestroy n'ont pas ce probleme (porteur
-- unique).
--
-- ANGLE MORT INDEPENDANT VERIFIE EN PREPARANT CE LOT (meme classe que Migrations/012, lu directement) :
-- Migrations/011_avatar_counters_and_bottles_writeback.sql, dernier script listant a la fois la forme du TYPE
-- ET celle des deux procedures dans la lignee que ce script prolonge, a recree game.tvp_CharacterProgress et
-- usp_Character_PersistProgressBatch/PersistFinalFlush SANS le parametre @Costumes / la transaction / le
-- remplacement de penderie que Migrations/008_autohunt_buffx2_premium_pet_writeback.sql avait a l'origine --
-- lu directement dans les deux scripts. CharacterRepository.PersistProgressAsync/PersistFinalFlushAsync
-- fournissent pourtant deja ce parametre des que la penderie n'est pas vide : usp_Character_PersistProgressBatch
-- echouerait alors avec un parametre inconnu. Restaure ici dans la meme passe.
-- Meme lecture directe : CharacterWorldSnapshotDto (CharacterDtos.cs) lit deja AutoTime/BuffX2Time en fin du
-- prefixe RS0 herite de Migrations/011, mais la projection reellement appliquee (011) ne les selectionne pas
-- -- CaeriusNet lit les colonnes du DataReader par ORDINAL (QueryMultipleReadOnlyCollectionAsync), donc
-- GetWorldEntryBundleAsync echouerait (IndexOutOfRange) a CHAQUE entree en monde. Restaure ici aussi.
--
-- AVERTISSEMENT DE SEQUENCAGE -- SURFACE ACTUELLEMENT DISPUTEE PAR PLUSIEURS LOTS CONCURRENTS
-- Au moment ou ce script est ecrit, Fenrir.Data.Abstractions.Characters.CharacterProgressTvp et
-- CharacterWorldSnapshotDto portent DEJA, en queue, des colonnes d'au moins quatre autres lots non encore
-- reconcilies avec aucun script SQL coherent (repere par lecture directe du disque, non journalise ici en
-- detail) : un groupe RankPoint/CloakLuckyBoxPity/CloakVariantBoxPity/MountVariantBoxPity, un groupe
-- ImproveItemValue/AddItemValue/HighItemValue/TaiyanKeyTimer (Migrations/032_progress_writeback_
-- reconciliation_and_item_value_counters.sql, lui-meme deja partiellement en collision de numerotation avec
-- Migrations/032_playtime_petbagdate_hsbreward_writeback.sql), et un groupe ProtectForRefine/ProtectForDestroy/
-- ProtectForCostume/ProtectForDestroy2/LodRounds/StellarCoreExpireDate. AUCUN de ces groupes n'est repris ici :
-- ce script prolonge la forme STRICTEMENT TERMINALE de Migrations/012_autohunt_premium_pet_writeback_restore.sql
-- (53 colonnes de 011 + les six restaurees par 012 = 59), a laquelle il ajoute ses cinq colonnes en queue (64
-- au total), et ne pretend PAS declarer l'union complete du record C# tel qu'il existe au moment de l'ecriture.
-- Une passe de reconciliation dediee (meme motif que 012 pour 011/008) sera necessaire pour fondre ce script
-- avec les groupes ci-dessus une fois qu'ils auront chacun leur propre script SQL stable -- ne pas le sauter,
-- sous peine de repeter la regression 008->011 une quatrieme fois.
--
-- POURQUOI UN NOUVEAU SCRIPT PLUTOT QU'UNE EDITION
-- Migrations/008/010/011/012 et les fichiers de base sont deja journalises SHA-256. Un type TABLE ne s'ALTERe
-- pas et ne se DROP pas tant qu'une procedure le prend en parametre : DROP+RECREATE integral, meme sequence
-- que 011/012.

-- ---------------------------------------------------------------------------
-- 1. game.Characters : cinq nouvelles colonnes. Valeur sure pour les lignes existantes : 0 partout (aucun
--    ticket consomme, l'etat d'un avatar qui n'a jamais utilise le parchemin/objet correspondant).
-- ---------------------------------------------------------------------------
IF COL_LENGTH('game.Characters', 'EliteDungeonTime') IS NULL
    ALTER TABLE game.Characters
        ADD EliteDungeonTime INT NOT NULL
                CONSTRAINT DF_Characters_EliteDungeonTime DEFAULT 0
                CONSTRAINT CK_Characters_EliteDungeonTime CHECK (EliteDungeonTime >= 0),
            DungeonKeyTime    INT NOT NULL
                CONSTRAINT DF_Characters_DungeonKeyTime DEFAULT 0
                CONSTRAINT CK_Characters_DungeonKeyTime CHECK (DungeonKeyTime >= 0),
            IvyHallTicketTime INT NOT NULL
                CONSTRAINT DF_Characters_IvyHallTicketTime DEFAULT 0
                CONSTRAINT CK_Characters_IvyHallTicketTime CHECK (IvyHallTicketTime >= 0),
            ScrollOfSeekersTime INT NOT NULL
                CONSTRAINT DF_Characters_ScrollOfSeekersTime DEFAULT 0
                CONSTRAINT CK_Characters_ScrollOfSeekersTime CHECK (ScrollOfSeekersTime >= 0),
            FightingGodForDestroy INT NOT NULL
                CONSTRAINT DF_Characters_FightingGodForDestroy DEFAULT 0
                CONSTRAINT CK_Characters_FightingGodForDestroy CHECK (FightingGodForDestroy >= 0);
GO

-- ---------------------------------------------------------------------------
-- 2. Les deux procedures qui referencent le type bloquent son DROP : les supprimer d'abord, puis le type.
-- ---------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS game.usp_Character_PersistFinalFlush;
DROP PROCEDURE IF EXISTS game.usp_Character_PersistProgressBatch;
DROP TYPE IF EXISTS game.tvp_CharacterProgress;
GO

-- ---------------------------------------------------------------------------
-- 3. Le type : forme TERMINALE de Migrations/012 (59 colonnes -- 53 de 011 + les six restaurees par 012),
--    plus les cinq de ce lot en toute derniere position. Cinq compteurs mono-proprietaires de
--    PlayerRuntimeState, meme categorie que DropItemTime/ProtectForHalo -- jamais un solde partage.
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
    SkillPoints               INT          NOT NULL,
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
    PetActivity               INT          NOT NULL,
    EliteDungeonTime         INT          NOT NULL,
    DungeonKeyTime           INT          NOT NULL,
    IvyHallTicketTime        INT          NOT NULL,
    ScrollOfSeekersTime      INT          NOT NULL,
    FightingGodForDestroy    INT          NOT NULL
);
GO

-- ---------------------------------------------------------------------------
-- 4. Le flush periodique. @Costumes/la transaction/le remplacement de penderie borne par @Applied restaures
--    depuis Migrations/008 (perdus par 010/011/012) ; CharacterRepository.PersistProgressAsync les fournit
--    deja. Garde d'idempotence (FlushSequence strictement croissante) inchangee.
-- ---------------------------------------------------------------------------
CREATE PROCEDURE game.usp_Character_PersistProgressBatch @Progress game.tvp_CharacterProgress READONLY,
                                                         @Costumes game.tvp_CharacterCostumeSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Applied TABLE (CharacterId INT NOT NULL PRIMARY KEY);

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
        c.EliteDungeonTime         = s.EliteDungeonTime,
        c.DungeonKeyTime           = s.DungeonKeyTime,
        c.IvyHallTicketTime        = s.IvyHallTicketTime,
        c.ScrollOfSeekersTime      = s.ScrollOfSeekersTime,
        c.FightingGodForDestroy    = s.FightingGodForDestroy,
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

    DECLARE @Applied TABLE (CharacterId INT NOT NULL PRIMARY KEY);

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
        c.EliteDungeonTime         = p.EliteDungeonTime,
        c.DungeonKeyTime           = p.DungeonKeyTime,
        c.IvyHallTicketTime        = p.IvyHallTicketTime,
        c.ScrollOfSeekersTime      = p.ScrollOfSeekersTime,
        c.FightingGodForDestroy    = p.FightingGodForDestroy,
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
-- 6. Le chemin de LECTURE. RS0 append-only en queue, mappe par ORDINAL sur CharacterWorldSnapshotDto :
--    AutoTime/BuffX2Time (deja lus cote C#, jamais projetes cote SQL depuis Migrations/011 -- angle mort
--    independant decrit en tete de script) puis les cinq nouvelles colonnes de ce lot se posent tout en fin.
--    Prefixe stable de 19 colonnes partage avec usp_Character_GetForWorldEntrySummary intact ; les quatre
--    autres result sets repris verbatim.
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
           c.EliteDungeonTime,
           c.DungeonKeyTime,
           c.IvyHallTicketTime,
           c.ScrollOfSeekersTime,
           c.FightingGodForDestroy
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
