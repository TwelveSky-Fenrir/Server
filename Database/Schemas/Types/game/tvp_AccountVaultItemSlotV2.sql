-- TVP V2 pour le coffre de compte : identique a game.tvp_AccountVaultItemSlot plus les quatre colonnes que
-- Migrations/004_accountvault_sockets_expiry.sql ajoute a game.AccountVaultItems -- les trois gemmes de
-- sertissage et la date d'expiration, que le coffre perdait jusqu'ici (INV-B2).
--
-- POURQUOI UN TYPE V2 PLUTOT QU'UN ALTER
-- Un type TABLE ne s'ALTERe pas, et il ne se DROPpe pas tant qu'une procedure le prend en parametre. Ici
-- TROIS procedures referencent game.tvp_AccountVaultItemSlot (usp_AccountVault_SetItems,
-- usp_AccountVault_TransferItemWithCharacter, et la lecture symetrique usp_AccountVault_Get pour la forme
-- des colonnes) : un drop+recreate a la maniere de Migrations/002 imposerait de reecrire ces procedures dans
-- le meme lot. Le type V2 est donc introduit a cote, le type V1 reste vivant et intact, et les procedures
-- basculent une par une dans le lot suivant. Aucun appelant existant n'est casse par ce fichier.
--
-- ORDRE DES COLONNES : le prefixe V1 est conserve tel quel et les quatre nouvelles sont AJOUTEES EN FIN,
-- meme posture que les cinq colonnes Mount* ajoutees a game.tvp_CharacterProgress par Migrations/002. La
-- procedure V2 pourra donc etre ecrite comme un sur-ensemble strict de la V1.
--
-- Trois gemmes exactement, pas quatre : uSaveSocket[MAX_SAVE_ITEM_SLOT_NUM][3]
-- (Server/Header/Protocol/STRUCT.h:462, MAX_SAVE_ITEM_GEM_NUM = 3 Server/Header/Protocol/DEFINE.h:313), et
-- CopyGSocket copie les 12 octets d'un bloc dans l'ordre gem1/gem2/gem3
-- (Server/ts25zone/S04_MyWork05.cpp:42-49). ExpireDate est l'entier legacy YYYYMMDD verbatim (0 = pas une
-- location), meme convention que game.CharacterItems.ExpireDate.
--
-- SocketData NVARCHAR(50) est conserve tel quel : c'est le champ de la V1, jamais alimente par le serveur
-- (toujours NULL), garde ici uniquement pour que la V2 reste un sur-ensemble de la V1. Les gemmes passent
-- par les trois colonnes INT typees, pas par cette chaine.
--
-- Quantity reste INT alors que game.AccountVaultItems.Quantity l'est aussi -- meme largeur des deux cotes,
-- pas de retrecissement implicite ici, contrairement a game.tvp_CharacterItemSlot.
CREATE TYPE game.tvp_AccountVaultItemSlotV2 AS TABLE
(
    SlotIndex    SMALLINT     NOT NULL,
    ItemId       INT          NULL,
    Quantity     INT          NOT NULL,
    Value        INT          NOT NULL,
    SerialNumber INT          NOT NULL,
    SocketData   NVARCHAR(50) NULL,
    SocketGem1   INT          NOT NULL,
    SocketGem2   INT          NOT NULL,
    SocketGem3   INT          NOT NULL,
    ExpireDate   INT          NOT NULL
);
