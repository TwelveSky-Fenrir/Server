-- Lot 13 -- quatre compteurs mutes en jeu (TimedBuffCountdownSystem.cs), persistes par le legacy, absents de
-- Fenrir : AnimalDoubleExp, DmgBoost, HPBoost, CriBoost.
--
-- CE QUE FAIT LE LEGACY (CreateAvatarColumn = Server/Header/CSQLAvatar.cpp:556-756, liste UNIQUE partagee par
-- SELECT/INSERT/UPDATE, donc tout ce qui y figure est reecrit au meme UPDATE d'avatar que le reste de
-- l'AVATAR_INFO -- Server/ts25playuser/S08_MyDB.cpp:99)
--   AnimalDoubleExp  aAnimalDoubleExp, CSQLAvatar.cpp:649 (FIELD_AVATAR0). Minutes restantes de double exp de
--                    monture, decrementees Server/ts25zone/S07_MyGame04.cpp:986-998 (garde USE_ANIMAL, allumee).
--   DmgBoost         aDmgBoost, CSQLAvatar.cpp:663. +10% attaque hors zone 124 (MyFactor.cpp:2235-2237).
--   HPBoost          aHPBoost, CSQLAvatar.cpp:664. Vie max x1.2 hors zone 124 (MyFactor.cpp:1740-1742).
--   CriBoost         aCriBoost, CSQLAvatar.cpp:665. +5 critique hors zone 124 (MyFactor.cpp:2933-2935).
-- Les quatre colonnes existent au schema legacy (Server/BuildEU33/DB/nxtserver.sql) et sont decrementees par
-- minute cote Fenrir (TimedBuffCountdownSystem.TickGroupB), mais aucune n'atteint jamais SQL : absentes de
-- game.Characters, de game.tvp_CharacterProgress et de Fenrir.Data.Abstractions.Characters.CharacterProgressTvp.
-- DoubleExpTime1/DoubleExpTime2 partagent le meme diagnostic (meme systeme, meme fichier, decrement
-- TickGroupA) mais N'ONT PAS BESOIN de ce script : leurs colonnes existent deja sur game.Characters
-- (Tables/game/Characters.sql:106-109) et sont deja projetees par usp_Character_GetForWorldEntry -- seul le
-- reste de leur chemin (TVP + PlayerEnterData) manque, et ce reste n'est PAS touche ici, voir plus bas.
--
-- CE QUE CE SCRIPT FAIT, ET SEULEMENT CELA
-- Ajoute les quatre colonnes reellement absentes a game.Characters, avec DEFAULT 0 (aucun octroi de depart
-- legacy pour ces quatre-la, contrairement a DoubleExpTime1/2 qui seedent a 300 -- Server/ts25login/
-- S04_MyWork02.cpp:886-887 -- ce script ne touche pas ce seed non plus). C'est une operation de metadonnees
-- pure sur une colonne NOT NULL avec DEFAULT : lignes existantes preservees a 0, valeur sure ici puisque 0
-- signifie "aucun boost actif", exactement l'etat d'un personnage neuf.
--
-- CE QUE CE SCRIPT NE FAIT PAS -- ET POURQUOI (BLOQUE, PAS OUBLIE)
-- Il n'etend PAS game.tvp_CharacterProgress, ni usp_Character_PersistProgressBatch/usp_Character_
-- PersistFinalFlush, ni la projection RS0 de usp_Character_GetForWorldEntry, ni Fenrir.Data.Abstractions.
-- Characters.CharacterProgressTvp/CharacterWorldSnapshotDto, ni PlayerEnterData/EnterWorldService/
-- Zone.PlayerLifecycle/AvatarInfoFactory. Au moment ou ce script est ecrit, ces objets partagent tous le
-- meme point de contention -- le mapper TVP/DTO genere par CaeriusNet lie POSITIONNELLEMENT (confirme via
-- obj/generated/.../CharacterProgressTvp.Tvp.g.cs et CharacterWorldSnapshotDto.Dto.g.cs) -- et SONT EN COURS
-- DE MODIFICATION CONCURRENTE PAR AU MOINS TROIS AUTRES LOTS DEJA IDENTIFIES SUR LE DISQUE AU MOMENT DE
-- L'ECRITURE DE CE SCRIPT :
--   Migrations/012_autohunt_premium_pet_writeback_restore.sql        (restaure AutoTime/AutoTime2/BuffX2Time/
--                                                                      PremiumExpireUtc/PetGrowth/PetActivity)
--   Migrations/032_playtime_petbagdate_hsbreward_writeback.sql       (ajoute PlayTime1/PlayTime3/
--                                                                      HsbStoneRewardClaimed, restaure aussi
--                                                                      AutoTime/BuffX2Time + @Costumes)
--   Migrations/033_holystone_rankpoint_and_lootbox_pity_writeback.sql (ajoute RankPoint/CloakLuckyBoxPity/
--                                                                      CloakVariantBoxPity/MountVariantBoxPity)
--   + un lot "ImproveItemValue/AddItemValue/HighItemValue/TaiyanKeyTimer" (cf. commentaire Lot 9 dans
--     _manifest.txt) dont le script n'est plus present sur disque au moment de l'ecriture (renomme/en cours).
-- Fenrir.Data.Abstractions.Characters.CharacterProgressTvp et CharacterWorldSnapshotDto (etat du disque au
-- moment de l'ecriture) portent DEJA les colonnes des quatre lots ci-dessus alors qu'AUCUNE migration
-- actuellement sur disque ne recree le type/la projection avec l'union complete des quatre -- la prochaine
-- entree en monde de n'importe quel personnage leve donc deja un IndexOutOfRangeException independamment de
-- ce script (CharacterWorldSnapshotDto lit par ordinal fixe jusqu'a TaiyanKeyTimer, ordinal that RS0 ne
-- fournit pas encore). Ecrire ici un cinquieme DROP+CREATE de game.tvp_CharacterProgress ne ferait
-- qu'ajouter une CINQUIEME version concurrente candidate a la course, avec un risque reel de collision de
-- numero de migration ET de types SQL devines a l'aveugle pour des colonnes appartenant a d'autres lots
-- (RankPoint/Cloak*/Improve*/Add*/High*/TaiyanKeyTimer n'ont pas ete verifies ici). La regle du depot est
-- justement d'etendre l'existant plutot que de creer un chemin parallele qui finit par diverger -- la
-- diverging le plus surement ici serait d'ecrire une sixieme forme concurrente du meme type sans visibilite
-- sur les cinq autres en vol.
--
-- CE QUI RESTE A FAIRE, POUR LE LOT QUI FERA LA RECONCILIATION FINALE DE game.tvp_CharacterProgress
-- Quand la course ci-dessus sera retombee (un seul DROP+CREATE qui declare enfin l'union complete de TOUS
-- les lots), ajouter en QUEUE, dans cet ordre, miroir exact de la fin de CharacterProgressTvp.cs a ce
-- moment-la :
--   DoubleExpTime1 INT NOT NULL, DoubleExpTime2 INT NOT NULL,   -- colonnes deja presentes sur game.Characters
--   AnimalDoubleExp INT NOT NULL, DmgBoost INT NOT NULL, HPBoost INT NOT NULL, CriBoost INT NOT NULL
--                                                                -- colonnes ajoutees par CE script
-- et les six clauses SET correspondantes (absolues, PlayerRuntimeState est proprietaire unique, meme
-- categorie que DropItemTime) dans usp_Character_PersistProgressBatch ET usp_Character_PersistFinalFlush, et
-- les six colonnes en queue de la projection RS0 de usp_Character_GetForWorldEntry (DoubleExpTime1/2 y sont
-- deja projetees plus haut dans la liste -- ne pas les dupliquer, seulement AnimalDoubleExp/DmgBoost/HPBoost/
-- CriBoost sont neuves cote lecture). Cote application, PlayerEnterData (Domain/World/ZoneCommand.cs),
-- EnterWorldService.cs (CompleteWorldEntryAsync, pres de PetExpX2Time) et Zone.PlayerLifecycle.cs
-- (HandleEnter, pres de state.PetExpX2Time = data.PetExpX2Time) ont chacun besoin des six memes champs ;
-- AvatarInfoFactory.cs a deja DoubleExpTime1/DoubleExpTime2 cables (character.DoubleExpTime1/2), il ne
-- manque que AnimalDoubleExp/DmgBoost/HPBoost/CriBoost (AvatarInfo expose deja ces quatre proprietes
-- required -- Fenrir.Core/Packets/Shared/AvatarInfo.cs -- silencieusement zerees par AvatarInfoTemplates.Zeroed
-- faute d'override).

-- Les quatre colonnes reellement absentes. Gardes d'idempotence : le migrateur journalise par SHA-256, mais
-- ce script doit rester rejouable a la main sur une base a l'etat inconnu.
IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'AnimalDoubleExp')
    ALTER TABLE game.Characters
        ADD AnimalDoubleExp INT NOT NULL
            CONSTRAINT DF_Characters_AnimalDoubleExp DEFAULT 0
            CONSTRAINT CK_Characters_AnimalDoubleExp CHECK (AnimalDoubleExp >= 0); -- aAnimalDoubleExp (Server/Header/CSQLAvatar.cpp:649), minutes restantes de double exp de monture
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'DmgBoost')
    ALTER TABLE game.Characters
        ADD DmgBoost INT NOT NULL
            CONSTRAINT DF_Characters_DmgBoost DEFAULT 0
            CONSTRAINT CK_Characters_DmgBoost CHECK (DmgBoost >= 0); -- aDmgBoost (Server/Header/CSQLAvatar.cpp:663), minutes restantes de +10% attaque hors zone 124
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'HPBoost')
    ALTER TABLE game.Characters
        ADD HPBoost INT NOT NULL
            CONSTRAINT DF_Characters_HPBoost DEFAULT 0
            CONSTRAINT CK_Characters_HPBoost CHECK (HPBoost >= 0); -- aHPBoost (Server/Header/CSQLAvatar.cpp:664), minutes restantes de vie max x1.2 hors zone 124
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'CriBoost')
    ALTER TABLE game.Characters
        ADD CriBoost INT NOT NULL
            CONSTRAINT DF_Characters_CriBoost DEFAULT 0
            CONSTRAINT CK_Characters_CriBoost CHECK (CriBoost >= 0); -- aCriBoost (Server/Header/CSQLAvatar.cpp:665), minutes restantes de +5 critique hors zone 124
GO
