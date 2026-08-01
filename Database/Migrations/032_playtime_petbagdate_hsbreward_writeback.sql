-- Lot 11 -- PetBagDate (write path), PlayTime1, PlayTime3, HsbStoneRewardClaimed : quatre champs mutes/charges
-- cote Fenrir, aucun jamais reecrit en base.
--
-- CE QUE FAIT LE LEGACY (verifie ligne a ligne, CreateAvatarColumn = Server/Header/CSQLAvatar.cpp:556-756,
-- liste UNIQUE partagee par SELECT/INSERT/UPDATE, donc tout ce qui y figure est reecrit au meme UPDATE
-- d'avatar que le reste de l'AVATAR_INFO -- Server/ts25playuser/S08_MyDB.cpp:99)
--   PetBagDate  aPetBagDate,           CSQLAvatar.cpp:581 (FIELD_AVATAR0). Date d'expiration YYYYMMDD du sac
--               a pets (moitie haute, slots 10-19), prolongee de 30 jours par l'objet cash 829
--               (Server/ts25zone/S04_MyWork03.cpp:2719-2728) et clampee a l'entree de zone par
--               SetIntegerLow(avt->aPetBagDate, tNowDate, 0) (Server/ts25zone/S07_MyGame03.cpp:5692).
--   PlayTime1   aPlayTime1,            CSQLAvatar.cpp:560. Compteur cumulatif, incremente d'une unite tous
--               les 120 ticks (Server/ts25zone/S07_MyGame04.cpp:889-892) ; jamais relu par le gameplay
--               compile, uniquement ecrit -- statistique administrative.
--   PlayTime3   aPlayTime3,            CSQLAvatar.cpp:562. Meme increment que PlayTime1, meme bloc des 120
--               ticks (S07_MyGame04.cpp:894). Contraste voulu avec son voisin aPlayTime2 (STRUCT.h) :
--               PlayTime2 est remis a zero par SetAvatar cote legacy et reste donc deliberement EPHEMERE cote
--               Fenrir (PlayerRuntimeState.PlayTime2, Simulation/PlayTimeAccrualSystem.cs:21).
--   HsbStoneRewardClaimed  bHSBStoneRewardCheck,  CSQLAvatar.cpp:730 (champ AVATAR_INFO, STRUCT.h:567).
--               Verrou anti-rejeu du butin de la pierre-symbole : quand mWorldInfo->mTribeSymbolBattle vaut
--               1 et que le flag est != 1, le tueur du monstre-symbole recoit trois drops (items
--               1448/723/1073) puis le flag passe a 1 (Server/ts25zone/S07_MyGame02.cpp:2379-2385) ; le reset
--               planifie ne touche QUE les joueurs connectes (case 4000, S07_MyGame08.cpp:1311-1321), d'ou la
--               necessite de persister pour qu'un joueur hors ligne au reset retrouve son flag a 1 au retour.
--               Colonne bHSBStoneRewardCheck int(11) DEFAULT -1 (Server/BuildEU33/DB/nxtserver.sql:186) : -1
--               est un troisieme etat, distinct de 0 (verrou leve) et 1 (deja reclame) -- un personnage neuf
--               n'est ni verrouille ni explicitement reinitialise. Reproduit tel quel ci-dessous (INT DEFAULT
--               -1, pas BIT) meme si PlayerRuntimeState n'expose qu'un bool : le seul test legacy est "!= 1",
--               donc -1 et 0 sont comportementalement identiques et le bool Fenrir peut coder les deux sur
--               false sans rien perdre.
--
-- L'ECART FENRIR (avant ce script)
--   PetBagDate  colonne et lecture DEJA en place (game.Characters.PetBagDate, projetee par
--               usp_Character_GetForWorldEntry, chargee jusque dans PlayerRuntimeState.PetBagDate --
--               Zone.PlayerLifecycle.cs:206). AUCUN chemin d'ecriture : absente de
--               game.tvp_CharacterProgress ET de Fenrir.Data.Abstractions.CharacterProgressTvp.
--   PlayTime1/3 aucune colonne, aucun TVP, aucun DTO. PlayerRuntimeState.PlayTime1/PlayTime3 sont incrementes
--               par Simulation/PlayTimeAccrualSystem.cs et reinitialises a 0 a chaque entree en monde.
--   HsbStoneReward  aucune colonne. PlayerRuntimeState.HsbStoneRewardClaimed n'est aujourd'hui remis qu'a
--               false par HsbRewardFlagResetReactor -- aucun code Fenrir ne le passe encore a true (le drop
--               de la pierre-symbole de S07_MyGame02.cpp:2379-2385 n'est pas encore porte), mais le flag est
--               deja un membre mutable de PlayerRuntimeState et doit survivre a la deconnexion des qu'un
--               futur lot le mettra a true.
--
-- CE QUE CE SCRIPT FAIT, ET SEULEMENT CELA
-- Ajoute les trois colonnes reellement absentes a game.Characters (PetBagDate existe deja depuis
-- Migrations/002, rien a faire pour elle ici -- seul son chemin d'ECRITURE manque, voir plus bas). Operation
-- de metadonnees pure sur une colonne NOT NULL avec DEFAULT : lignes existantes preservees a la valeur par
-- defaut -- sure ici (0 pour les deux compteurs cumulatifs, -1 pour HsbStoneRewardClaimed, la valeur legacy
-- de depart, DEFAULT -1 reproduit ci-dessus).
--
-- CE QUE CE SCRIPT NE FAIT PAS -- ET POURQUOI (BLOQUE, PAS OUBLIE)
-- Il n'etend PAS game.tvp_CharacterProgress, ni usp_Character_PersistProgressBatch/usp_Character_
-- PersistFinalFlush, ni la projection RS0 de usp_Character_GetForWorldEntry, ni Fenrir.Data.Abstractions.
-- Characters.CharacterProgressTvp/CharacterWorldSnapshotDto, ni PlayerEnterData/EnterWorldService/
-- Zone.PlayerLifecycle. Au moment ou ce script est ecrit, ces objets partagent tous le meme point de
-- contention -- le mapper TVP/DTO genere par CaeriusNet lie POSITIONNELLEMENT -- et sont EN COURS DE
-- MODIFICATION CONCURRENTE PAR PLUSIEURS AUTRES LOTS DEJA IDENTIFIES SUR LE DISQUE AU MOMENT DE L'ECRITURE DE
-- CE SCRIPT :
--   Migrations/012_autohunt_premium_pet_writeback_restore.sql        (restaure AutoTime/AutoTime2/BuffX2Time/
--                                                                      PremiumExpireUtc/PetGrowth/PetActivity,
--                                                                      regression de Migrations/010 et 011 qui
--                                                                      s'etaient chacune rebasees sur
--                                                                      Migrations/007 au lieu de 008)
--   "032_progress_writeback_reconciliation_and_item_value_counters.sql" (Lot 9, decrit dans _manifest.txt,
--                                                                      absent du disque au moment de
--                                                                      l'ecriture -- ajoute ImproveItemValue/
--                                                                      AddItemValue/HighItemValue/
--                                                                      TaiyanKeyTimer et pretend reconcilier
--                                                                      le meme angle mort que 012 ci-dessus)
--   Migrations/033_holystone_rankpoint_and_lootbox_pity_writeback.sql (Lot 10, ajoute RankPoint/
--                                                                      CloakLuckyBoxPity/CloakVariantBoxPity/
--                                                                      MountVariantBoxPity, rebase explicitement
--                                                                      sur la forme terminale annoncee du Lot 9
--                                                                      ci-dessus sans que son fichier existe)
--   Migrations/034_animal_double_exp_and_combat_boost_columns.sql     (Lot 13, ajoute AnimalDoubleExp/DmgBoost/
--                                                                      HPBoost/CriBoost, colonnes seulement,
--                                                                      meme constat de contention que ce script)
-- Un sixieme DROP+CREATE de game.tvp_CharacterProgress ecrit ici, a l'aveugle sur les types SQL exacts que
-- les quatre lots ci-dessus attribuent a LEURS propres colonnes, ne ferait qu'ajouter une candidate
-- supplementaire a une course deja a cinq versions concurrentes -- avec un risque reel qu'une DROP+CREATE
-- plus tardive efface silencieusement celle-ci ou l'inverse. La regle du depot est d'etendre l'existant
-- plutot que de creer un chemin parallele qui finit par diverger ; ici, diverger le plus surement serait
-- d'ecrire une SIXIEME forme concurrente du meme type sans visibilite sur les cinq autres en vol.
--
-- CE QUI RESTE A FAIRE, POUR LE LOT QUI FERA LA RECONCILIATION FINALE DE game.tvp_CharacterProgress
-- Quand la course ci-dessus sera retombee (un seul DROP+CREATE qui declare enfin l'union complete de tous les
-- lots), ajouter en QUEUE, miroir exact de la fin de CharacterProgressTvp.cs a ce moment-la :
--   PetBagDate INT NOT NULL, PlayTime1 INT NOT NULL, PlayTime3 INT NOT NULL,
--   HsbStoneRewardClaimed INT NOT NULL
-- et les quatre clauses SET correspondantes (absolues, PlayerRuntimeState est proprietaire unique, meme
-- categorie que DropItemTime -- HsbStoneRewardClaimed s'ecrit "state.HsbStoneRewardClaimed ? 1 : 0" cote C#)
-- dans usp_Character_PersistProgressBatch ET usp_Character_PersistFinalFlush, et les quatre colonnes en queue
-- de la projection RS0 de usp_Character_GetForWorldEntry (c.PetBagDate y est DEJA projete plus haut dans la
-- liste depuis Migrations/002 -- ne pas le dupliquer, seuls PlayTime1/PlayTime3/HsbStoneRewardClaimed sont
-- neufs cote lecture). Cote application, PlayerEnterData (Domain/World/ZoneCommand.cs -- PetBagDate y figure
-- deja, ajouter PlayTime1/PlayTime3/HsbStoneRewardClaimed a cote), EnterWorldService.cs (mapper
-- character.PlayTime1/PlayTime3/(character.HsbStoneRewardClaimed == 1) pres du mapping PetBagDate existant),
-- Zone.PlayerLifecycle.cs (state.PlayTime1 = data.PlayTime1 etc., pres de l'affectation PetBagDate existante
-- a la ligne 206) et ProgressWriteBehindHost.cs/PositionWriteBehindHost.cs (ajouter les quatre arguments
-- nommes state.PetBagDate/state.PlayTime1/state.PlayTime3/(state.HsbStoneRewardClaimed ? 1 : 0) a la
-- construction de CharacterProgressTvp) ont chacun besoin des memes quatre champs.
--
-- POURQUOI UN NOUVEAU SCRIPT PLUTOT QU'UNE EDITION DES FICHIERS DE BASE
-- Tables/game/Characters.sql est deja journalise SHA-256 par Fenrir.Tools.DbMigrator sur toute base qui l'a
-- applique une fois -- le migrateur refuse de re-appliquer un chemin journalise dont le contenu a change.
--
-- POURQUOI ALTER ADD ET NON AUTRE CHOSE
-- game.Characters porte de vraies donnees joueur : un ALTER additif avec DEFAULT sur une colonne NOT NULL est
-- une operation de metadonnees seule sur SQL Server, lignes existantes preservees a la valeur par defaut.
-- Aucun IHostedService n'est ajoute ni enregistre : ProgressWriteBehindHost/PositionWriteBehindHost existants
-- suffiront, seuls leurs appels TVP devront etre etendus par le lot de reconciliation finale ci-dessus.

-- Les trois colonnes reellement absentes. Gardes d'idempotence : le migrateur journalise par SHA-256, mais ce
-- script doit rester rejouable a la main sur une base a l'etat inconnu.
IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'PlayTime1')
ALTER TABLE game.Characters
    ADD PlayTime1 INT NOT NULL
        CONSTRAINT DF_Characters_PlayTime1 DEFAULT 0
        CONSTRAINT CK_Characters_PlayTime1 CHECK (PlayTime1 >= 0); -- aPlayTime1 (Server/Header/CSQLAvatar.cpp:560), compteur cumulatif de minutes jouees, jamais relu par le gameplay compile
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'PlayTime3')
ALTER TABLE game.Characters
    ADD PlayTime3 INT NOT NULL
        CONSTRAINT DF_Characters_PlayTime3 DEFAULT 0
        CONSTRAINT CK_Characters_PlayTime3 CHECK (PlayTime3 >= 0); -- aPlayTime3 (Server/Header/CSQLAvatar.cpp:562), meme increment que PlayTime1 (Server/ts25zone/S07_MyGame04.cpp:889-894)
GO

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID(N'game.Characters')
                 AND name = N'HsbStoneRewardClaimed')
ALTER TABLE game.Characters
    ADD HsbStoneRewardClaimed INT NOT NULL
        CONSTRAINT DF_Characters_HsbStoneRewardClaimed DEFAULT -1
        CONSTRAINT CK_Characters_HsbStoneRewardClaimed CHECK (HsbStoneRewardClaimed BETWEEN -1 AND 1); -- bHSBStoneRewardCheck (Server/Header/CSQLAvatar.cpp:730), DEFAULT -1 tel que nxtserver.sql:186 : verrou anti-rejeu du butin de la pierre-symbole (Server/ts25zone/S07_MyGame02.cpp:2379-2385)
GO
