using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.AvatarRoster,
    Obfuscation = WireObfuscationMode.XorFieldAvatar, ExpectedSize = 4579)]
public readonly partial record struct AvatarRosterResponse : IOutgoingPacket
{
    [AvatarXorKind(AvatarXorKind.Int)] public required int VisibleState { get; init; }
    [AvatarXorKind(AvatarXorKind.Int)] public required int SpecialState { get; init; }
    [AvatarXorKind(AvatarXorKind.Int)] public required int CostumeIndex { get; init; }
    [AvatarXorKind(AvatarXorKind.Int)] public required int Tribe { get; init; }
    [AvatarXorKind(AvatarXorKind.Int)] public required int PreviousTribe { get; init; }
    [AvatarXorKind(AvatarXorKind.Int)] public required int EatLifePotion { get; init; }
    [AvatarXorKind(AvatarXorKind.Int)] public required int Gender { get; init; }
    [AvatarXorKind(AvatarXorKind.Int)] public required int HeadType { get; init; }
    [AvatarXorKind(AvatarXorKind.Int)] public required int FaceType { get; init; }
    [AvatarXorKind(AvatarXorKind.Int)] public required int EatStrPotion { get; init; }
    [AvatarXorKind(AvatarXorKind.Int)] public required int Level1 { get; init; }

    [FixedArray(768)]
    [AvatarXorKind(AvatarXorKind.IntArray)]
    public required int[] Inventory { get; init; }

    [AvatarXorKind(AvatarXorKind.Int)] public required int Level2 { get; init; }
    [AvatarXorKind(AvatarXorKind.Int)] public required int EatManaPotion { get; init; }
    [AvatarXorKind(AvatarXorKind.Int)] public required int Halo { get; init; }
    [AvatarXorKind(AvatarXorKind.Int)] public required int RebirthNum { get; init; }
    [AvatarXorKind(AvatarXorKind.Int)] public required int KillOtherTribe { get; init; }
    [AvatarXorKind(AvatarXorKind.Int)] public required int SkillPoint { get; init; }

    [FixedArray(52)]
    [AvatarXorKind(AvatarXorKind.IntArray)]
    public required int[] Equip { get; init; }

    [AvatarXorKind(AvatarXorKind.Int)] public required int EatDexPotion { get; init; }

    [FixedString(13)]
    [AvatarXorKind(AvatarXorKind.Char)]
    public required string Name { get; init; }

    [AvatarXorKind(AvatarXorKind.Int)] public required int EatElePotion { get; init; }

    [FixedArray(6)]
    [AvatarXorKind(AvatarXorKind.IntArray)]
    public required int[] LogoutInfo { get; init; }

    [FixedString(13)]
    [AvatarXorKind(AvatarXorKind.Char)]
    public required string GuildName { get; init; }

    [FixedArray(224)]
    [AvatarXorKind(AvatarXorKind.IntArray)]
    public required int[] StoreItem { get; init; }

    [FixedArray(20)]
    [AvatarXorKind(AvatarXorKind.IntArray)]
    public required int[] PetBag { get; init; }

    [FixedArray(10)]
    [FixedString(13)]
    [AvatarXorKind(AvatarXorKind.Char2, 13)]
    public required string[] Friend { get; init; }

    [FixedString(13)]
    [AvatarXorKind(AvatarXorKind.Char)]
    public required string Teacher { get; init; }

    [FixedString(13)]
    [AvatarXorKind(AvatarXorKind.Char)]
    public required string Student { get; init; }

    [FixedArray(10)]
    [AvatarXorKind(AvatarXorKind.IntArray)]
    public required int[] Costume { get; init; }
}
