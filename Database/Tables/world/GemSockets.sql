CREATE TABLE world.GemSockets
(
    GemSocketId INT NOT NULL,
    Type        INT NOT NULL,
    Value01     INT NOT NULL,
    Value02     INT NOT NULL,
    Value03     INT NOT NULL,
    Value04     INT NOT NULL,
    CONSTRAINT PK_GemSockets PRIMARY KEY CLUSTERED (GemSocketId),
    CONSTRAINT UQ_GemSockets_Type_Value02 UNIQUE (Type, Value02),
    CONSTRAINT CK_GemSockets_TypeBandRules CHECK (
        Value02 = 0
            OR Type = 0
            OR (
            Type BETWEEN 1 AND 46
                AND (
                (Type = 1 AND Value02 BETWEEN 1 AND 33 AND Value03 BETWEEN 0 AND 400 AND Value04 BETWEEN 0 AND 400)
                    OR (Type BETWEEN 2 AND 29 AND Value02 BETWEEN 1 AND 100 AND Value03 BETWEEN 0 AND 1000 AND
                        Value04 BETWEEN 0 AND 1000)
                    OR
                (Type BETWEEN 30 AND 38)
                    OR (Type BETWEEN 39 AND 42 AND Value02 BETWEEN 1 AND 10 AND Value03 >= 1 AND Value04 = 0)
                    OR (Type BETWEEN 43 AND 46 AND Value02 BETWEEN 1 AND 10 AND Value03 >= 6 AND Value04 = 0)
                )
            )
        )
);
