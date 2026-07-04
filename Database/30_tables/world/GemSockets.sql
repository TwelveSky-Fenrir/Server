-- Legacy SOCKET_INFO; GemSocketId is the 1-based array slot (the struct has no explicit index field).
-- UNIQUE(Type, Value02) mirrors the legacy lookup key: GSOCKET::Search(mType, mValue02) in ts25zone GameSystem_08_Socket.cpp.
CREATE TABLE world.GemSockets
(
    GemSocketId INT NOT NULL,
    Type        INT NOT NULL,
    Value01     INT NOT NULL,
    Value02     INT NOT NULL,
    Value03     INT NOT NULL,
    Value04     INT NOT NULL,
    CONSTRAINT PK_GemSockets PRIMARY KEY CLUSTERED (GemSocketId),
    CONSTRAINT UQ_GemSockets_Type_Value02 UNIQUE (Type, Value02)
);
