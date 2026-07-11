using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(1384)]
public readonly partial record struct WorldInfo : IFenrirWireType<WorldInfo>
{
    public required int Zone038WinTribe { get; init; }
    public required int Zone038WinTribeTime { get; init; }
    public required int TribeSymbolBattle { get; init; }
    [FixedArray(4)] public required int[] TribeSymbol { get; init; }
    public required int MonsterSymbol { get; init; }
    public required int MonsterSymbolEndTime { get; init; }
    [FixedArray(4)] public required int[] TribePoint { get; init; }
    [FixedArray(2)] public required int[] TribeCloseInfo { get; init; }

    [FixedArray(8)] public required int[] PossibleAllianceInfo { get; init; }

    [FixedArray(4)] public required int[] AllianceState { get; init; }

    [FixedArray(4)] public required int[] TribeVoteState { get; init; }
    [FixedArray(4)] public required int[] CloseVoteState { get; init; }
    public required int Tribe4QuestDate { get; init; }
    public required int Tribe4QuestState { get; init; }
    [FixedString(13)] public required string Tribe4QuestName { get; init; }
    [Reserved(3)] [FixedArray(13)] public required int[] Zone049TypeState { get; init; }
    [FixedArray(13)] public required int[] Zone049TypeStateTime { get; init; }
    [FixedArray(6)] public required int[] Zone051TypeState { get; init; }
    [FixedArray(6)] public required int[] Zone051TypeStateTime { get; init; }
    [FixedArray(10)] public required int[] Zone053TypeState { get; init; }
    [FixedArray(10)] public required int[] Zone053TypeStateTime { get; init; }

    [FixedArray(32)] public required int[] Zone175TypeState { get; init; }

    [FixedArray(16)] public required int[] TribeGuardState { get; init; }

    public required int Zone194TypeState { get; init; }
    [FixedArray(3)] public required int[] Zone297TypeState { get; init; }
    [FixedArray(3)] public required int[] Zone297TypeBegin { get; init; }
    [FixedArray(4)] public required int[] Zone038DTMValue { get; init; }
    [FixedArray(4)] public required float[] TribeGeneralExperienceUpRatioInfo { get; init; }
    [FixedArray(4)] public required float[] TribeItemDropUpRatioInfo { get; init; }
    [FixedArray(4)] public required float[] TribeItemDropUpRatioForMyoungInfo { get; init; }
    [FixedArray(4)] public required int[] TribeKillOtherTribeAddValueInfo { get; init; }
    [FixedArray(4)] public required int[] TribeMasterCallAbility { get; init; }
    [FixedArray(4)] public required int[] Zone267TypeState { get; init; }
    [FixedArray(20)] public required int[] Zone241TypeState { get; init; }
    [FixedArray(5)] public required int[] Zone270TypeState { get; init; }
    public required int GuildBattle { get; init; }
    [FixedString(13)] public required string GuildName1 { get; init; }
    [FixedString(13)] public required string GuildName2 { get; init; }

    [FixedArray(3)] [FixedString(13)] public required string[] GuildName3 { get; init; }

    [Reserved(3)] [FixedArray(3)] public required int[] GuildScore { get; init; }

    public required int Zone088WinTribe { get; init; }
    [FixedArray(4)] public required int[] FourGuildState { get; init; }

    [FixedArray(16)] [FixedString(13)] public required string[] FourGuildName { get; init; }

    [FixedArray(4)] [FixedString(13)] public required string[] DecideChallengeFourGuildName { get; init; }

    public required int Zone200TypeState { get; init; }
    [FixedArray(4)] public required int[] Zone5TypeState { get; init; }
    public required int Zone54TypeState { get; init; }
    public required int CountDrop8Cho { get; init; }
    [FixedArray(4)] public required int[] TribeNokSanStone { get; init; }
    [FixedArray(9)] public required int[] NokSanStoneState { get; init; }
    [FixedArray(5)] public required int[] Zone319TypeState { get; init; }
    public required int WaterRainHeavenState { get; init; }
    public required int WaterRainHeavenStart { get; init; }
    public required int ZoneFFATypeState { get; init; }
    [FixedArray(5)] public required int[] Unk6 { get; init; }
    [FixedArray(5)] public required int[] PopUpTypeState { get; init; }
    [FixedArray(5)] public required int[] PopUpKillAvt { get; init; }
    [FixedArray(5)] public required int[] PopUpKillMonster { get; init; }
}
