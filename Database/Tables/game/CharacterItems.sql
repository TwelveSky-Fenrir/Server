CREATE TABLE game.CharacterItems
(
    CharacterId INT      NOT NULL,
    Container   TINYINT  NOT NULL,
    Slot        TINYINT  NOT NULL,
    ItemId      INT      NOT NULL,
    Quantity    SMALLINT NOT NULL
        CONSTRAINT DF_CharacterItems_Quantity DEFAULT 1,
    Enchant     TINYINT  NOT NULL
        CONSTRAINT DF_CharacterItems_Enchant DEFAULT 0,
    Combine     TINYINT  NOT NULL
        CONSTRAINT DF_CharacterItems_Combine DEFAULT 0,
    Refine      TINYINT  NOT NULL
        CONSTRAINT DF_CharacterItems_Refine DEFAULT 0,
    Socket      TINYINT  NOT NULL
        CONSTRAINT DF_CharacterItems_Socket DEFAULT 0,
    SocketGem1  INT      NOT NULL
        CONSTRAINT DF_CharacterItems_SocketGem1 DEFAULT 0,
    SocketGem2  INT      NOT NULL
        CONSTRAINT DF_CharacterItems_SocketGem2 DEFAULT 0,
    SocketGem3  INT      NOT NULL
        CONSTRAINT DF_CharacterItems_SocketGem3 DEFAULT 0,
    ExpireDate  INT      NOT NULL
        CONSTRAINT DF_CharacterItems_ExpireDate DEFAULT 0,
    Serial      INT      NOT NULL
        CONSTRAINT DF_CharacterItems_Serial DEFAULT 0,
    CONSTRAINT PK_CharacterItems PRIMARY KEY CLUSTERED (CharacterId, Container, Slot),
    CONSTRAINT CK_CharacterItems_Quantity CHECK (Quantity BETWEEN 1 AND 999), 
    CONSTRAINT CK_CharacterItems_ContainerSlot CHECK (
        (Container IN (0, 1) AND Slot <= 63)                                  
            OR (Container = 2 AND Slot <= 12)                                 
            OR (Container IN (3, 4) AND Slot <= 27)),                         
    CONSTRAINT FK_CharacterItems_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT FK_CharacterItems_World_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
