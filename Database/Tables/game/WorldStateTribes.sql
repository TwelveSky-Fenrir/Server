CREATE TABLE game.WorldStateTribes
(
    TribeId       TINYINT      NOT NULL,
    SymbolDateUtc DATETIME2(3) NULL,
    HasSymbol     BIT          NOT NULL
        CONSTRAINT DF_WorldStateTribes_HasSymbol DEFAULT 1,
    Points        INT          NOT NULL
        CONSTRAINT DF_WorldStateTribes_Points DEFAULT 0,
    IsClosed      BIT          NOT NULL
        CONSTRAINT DF_WorldStateTribes_IsClosed DEFAULT 0,
    CONSTRAINT PK_WorldStateTribes PRIMARY KEY CLUSTERED (TribeId),
    CONSTRAINT FK_WorldStateTribes_Tribe FOREIGN KEY (TribeId) REFERENCES game.Tribes (TribeId)
);
