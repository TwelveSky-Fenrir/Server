namespace Fenrir.LoginServer.Network.Metadata;

public enum PacketType : byte
{
    Unknown = 0,
    Hello = 0x01,
    LoginRequest = 0x02,
    Ping = 0x03,
    ServerListRequest = 0x04,
    ConnectOk = 0x10, // B_CONNECT_OK (S05_MyTransfer.cpp)
    LoginRecv = 0x11, // B_LOGIN_RECV
    UserAvatarInfo = 0x12, // B_USER_AVATAR_INFO
    CreateMousePasswordRecv = 0x13, // B_CREATE_MOUSE_PASSWORD_RECV
    ChangeMousePasswordRecv = 0x14, // B_CHANGE_MOUSE_PASSWORD_RECV
    LoginMousePasswordRecv = 0x15, // B_LOGIN_MOUSE_PASSWORD_RECV
    LoginSecretCardRecv = 0x16, // B_LOGIN_SECRET_CARD_RECV
    CreateAvatarRecv = 0x17, // B_CREATE_AVATAR_RECV
    DeleteAvatarRecv = 0x18, // B_DELETE_AVATAR_RECV
    ChangeAvatarNameRecv = 0x19, // B_CHANGE_AVATAR_NAME_RECV
    DemandGiftRecv = 0x20, // B_DEMAND_GIFT_RECV
    ServerZoneInfo = 0x21, // B_DEMAND_ZONE_SERVER_INFO_1_RESULT
    RcmdWorldSend = 0x22, // B_RCMD_WORLD_SEND
    ItemEventSend = 0x23, // B_ITEM_EVENT_SEND

    ReceiveGiftItemSend = 0x24 // B_RECIVE_GIFT_ITEM_SEND
    // Ajoutez d'autres types de paquets ici si nécessaire
}