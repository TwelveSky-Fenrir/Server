CREATE TABLE game.WorldStateAllianceOffers
(
    FromTribeId TINYINT NOT NULL,
    ToTribeId   TINYINT NOT NULL,
    IsAccepted  BIT     NOT NULL
        CONSTRAINT DF_WorldStateAllianceOffers_IsAccepted DEFAULT 0,
    CONSTRAINT PK_WorldStateAllianceOffers PRIMARY KEY CLUSTERED (FromTribeId, ToTribeId),
    CONSTRAINT CK_WorldStateAllianceOffers_NotSelf CHECK (FromTribeId <> ToTribeId),
    CONSTRAINT FK_WorldStateAllianceOffers_FromTribe FOREIGN KEY (FromTribeId) REFERENCES game.Tribes (TribeId),
    CONSTRAINT FK_WorldStateAllianceOffers_ToTribe FOREIGN KEY (ToTribeId) REFERENCES game.Tribes (TribeId)
);
