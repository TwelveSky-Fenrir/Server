
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
