using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(632)]
public readonly record struct ObjectForAvatar : IFenrirWireType<ObjectForAvatar>
{
    public required int VisibleState { get; init; }
    public required int SpecialState { get; init; }
    public required int KillOtherTribe { get; init; }
    public required int GoodFellow { get; init; }
    [FixedString(13)] public required string GuildName { get; init; }
    [Reserved(3)] public required int GuildRole { get; init; }
    [FixedString(5)] public required string CallName { get; init; }
    [Reserved(3)] public required int GuildMarkEffect { get; init; }
    [FixedString(13)] public required string Name { get; init; }
    [Reserved(3)] public required int Tribe { get; init; }
    public required int PreviousTribe { get; init; }
    public required int Gender { get; init; }
    public required int HeadType { get; init; }
    public required int FaceType { get; init; }
    public required int Level1 { get; init; }
    public required int Level2 { get; init; }
    [FixedArray(26)] public required int[] EquipForView { get; init; }
    public required int AnimalNumber { get; init; }
    public required int Title { get; init; }
    public required int Halo { get; init; }
    public required int RebirthNum { get; init; }
    public required int BattleTeam { get; init; }
    public required ActionInfo Action { get; init; }
    public required int MaxLifeValue { get; init; }
    public required int LifeValue { get; init; }
    public required int MaxManaValue { get; init; }
    public required int ManaValue { get; init; }
    [FixedArray(35)] public required int[] EffectValueForView { get; init; }
    [FixedString(13)] public required string PartyName { get; init; }
    [Reserved(3)] [FixedArray(3)] public required int[] DuelState { get; init; }
    public required int PShopState { get; init; }
    [FixedString(25)] public required string PShopName { get; init; }
    [Reserved(3)] public required int CostumeNumber { get; init; }
    public required int BufEffectTimeState { get; init; }
    public required int BufSort { get; init; }
    public required int AutoState { get; init; }
    public required int FishingState { get; init; }
    public required int FishingStep { get; init; }
    [FixedArray(3)] public required float[] FishingPoint { get; init; }
    public required int RankPoint { get; init; }
    public required int TargetState { get; init; }
    public required int AnimalAbsorbState { get; init; }
    public required int PetValid { get; init; }
    public required int Unk1 { get; init; }
    [FixedArray(3)] public required float[] PetLocation { get; init; }
    public required float PetFrame { get; init; }
    public required int Unk624 { get; init; }
    public required int Unk625 { get; init; }
    public required int UniqueSkillNumber { get; init; }
    public required int UniqueSkillBuffTime { get; init; }
    public required int CostumeState { get; init; }
    public required int StellarCoreNumber { get; init; }
}
