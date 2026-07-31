-- INV-B2 -- le coffre de compte transporte enfin les gemmes de sertissage et la date d'expiration.
--
-- Le legacy porte les deux au travers du coffre, dans les deux sens :
--   * depot     Server/ts25zone/S04_MyWork05.cpp:2999-3000
--                 CopyGSocket(tITEM_INFO, wUSaveSocket[tIndex2], wInvenSocket[tPage1][tIndex1]);
--                 wAvatar.uSaveExpireDate[tIndex2] = wAvatar.aInvenExpireDate[tPage1][tIndex1];
--   * retrait   Server/ts25zone/S04_MyWork05.cpp:3104-3105 (symetrique, sens inverse)
--   * intra-coffre Server/ts25zone/S04_MyWork05.cpp:3195 (ProcessForSaveToSave)
-- Le stockage cible cote avatar est uSaveSocket[MAX_SAVE_ITEM_SLOT_NUM][3] (Server/Header/Protocol/STRUCT.h:462)
-- et uSaveExpireDate[MAX_SAVE_ITEM_SLOT_NUM] (Server/Header/Protocol/STRUCT.h:565) : exactement TROIS gemmes
-- par slot (MAX_SAVE_ITEM_GEM_NUM = 3, Server/Header/Protocol/DEFINE.h:313) plus UNE date, sur 28 slots
-- (MAX_SAVE_ITEM_SLOT_NUM = 28, Server/Header/Protocol/DEFINE.h:311). CopyGSocket fait un CopyMemory de 12
-- octets (Server/ts25zone/S04_MyWork05.cpp:42-49) : les trois int sont contigus et copies dans l'ordre
-- gem1, gem2, gem3 -- pas de reordonnancement, pas de quatrieme emplacement cache.
--
-- game.AccountVaultItems ne portait ni les gemmes ni l'expiration : un objet loue depose au coffre en
-- ressortait avec ExpireDate = 0 et un objet serti y perdait ses trois gemmes, de facon persistee et
-- irreversible. Les quatre colonnes ci-dessous ferment ce trou et sont l'exact miroir des colonnes
-- homonymes de game.CharacterItems (Tables/game/CharacterItems.sql:32-39), memes types, memes defauts :
-- l'objet est le meme des deux cotes du transfert, il n'a aucune raison de changer de forme en route.
-- ExpireDate reste l'entier legacy YYYYMMDD verbatim (0 = pas une location), meme convention que
-- game.CharacterItems.ExpireDate -- c'est le meme entier qui repart vers le client.
--
-- POURQUOI UN NOUVEAU SCRIPT PLUTOT QU'UNE EDITION DE Tables/game/AccountVaultItems.sql
-- Ce fichier de base est deja liste dans _manifest.txt, donc journalise par SHA-256 par
-- Fenrir.Tools.DbMigrator sur toute base qui l'a applique une fois (le volume de dev AppHost est persistant).
-- Le migrateur refuse de re-appliquer un chemin journalise dont le contenu a change : une edition en place
-- ferait echouer durement la prochaine execution. Une base FRAICHE cree donc la table sans ces colonnes puis
-- applique cette migration ; une base PERSISTANTE saute les scripts deja journalises et n'applique que
-- celle-ci. Les deux convergent sur la meme forme finale.
--
-- POURQUOI ALTER TABLE ... ADD ICI (contrairement a Migrations/001 et 002)
-- game.AccountVaultItems est une table utilisateur ordinaire, ni MEMORY_OPTIMIZED ni schema-bound, et aucune
-- procedure ne l'englobe dans une dependance qui bloquerait l'ajout. Un ALTER additif avec DEFAULT sur une
-- colonne NOT NULL est une operation de metadonnees seule sur SQL Server (Microsoft Learn, "Add Columns to a
-- Table") : pas de reecriture de pages, donnees existantes preservees. Surtout, la table porte de vraies
-- donnees joueur -- un DROP+CREATE comme en Migrations/001 detruirait les coffres.
--
-- CE QUE CE SCRIPT NE FAIT PAS
-- Il ne touche a AUCUNE procedure stockee. game.usp_AccountVault_Get, _SetItems et
-- _TransferItemWithCharacter listent leurs colonnes explicitement, donc l'ajout ne les casse pas : elles
-- continuent simplement d'ecrire les defauts 0 dans les quatre nouvelles colonnes. Leur passage en V2
-- (lecture et ecriture effectives des gemmes et de l'expiration, via le nouveau type
-- game.tvp_AccountVaultItemSlotV2) est un lot suivant. Tant qu'il n'est pas livre, ces colonnes existent
-- mais restent a 0 : la perte de donnees n'est pas encore refermee cote execution, seulement cote schema.

-- Garde d'idempotence sur la premiere colonne : les quatre sont ajoutees dans le meme ALTER, elles sont donc
-- soit toutes presentes soit toutes absentes.
IF COL_LENGTH('game.AccountVaultItems', 'SocketGem1') IS NULL
    ALTER TABLE game.AccountVaultItems
        ADD SocketGem1 INT NOT NULL
                CONSTRAINT DF_AccountVaultItems_SocketGem1 DEFAULT 0,
            SocketGem2 INT NOT NULL
                CONSTRAINT DF_AccountVaultItems_SocketGem2 DEFAULT 0,
            SocketGem3 INT NOT NULL
                CONSTRAINT DF_AccountVaultItems_SocketGem3 DEFAULT 0,
            ExpireDate INT NOT NULL
                CONSTRAINT DF_AccountVaultItems_ExpireDate DEFAULT 0;
GO
