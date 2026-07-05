namespace Fenrir.Network.Serialization.Packets.Shared;

// Wire-zero templates for world-state structs embedded in ZC_BROADCAST_WORLD_INFO/ZC_REGISTER_AVATAR_RECV;
// no persisted source to project from yet, so every field starts zeroed.
public static class WorldStateTemplates
{
    // Array lengths must match each property's own [FixedArray] attribute; string arrays must be
    // filled with "" not left as bare `new string[N]`, or the codec hits null entries.
    public static readonly WorldInfo ZeroedWorldInfo = new()
    {
        Zone038WinTribe = 0,
        Zone038WinTribeTime = 0,
        TribeSymbolBattle = 0,
        TribeSymbol = new int[4],
        MonsterSymbol = 0,
        MonsterSymbolEndTime = 0,
        TribePoint = new int[4],
        TribeCloseInfo = new int[2],
        PossibleAllianceInfo = new int[8],
        AllianceState = new int[4],
        TribeVoteState = new int[4],
        CloseVoteState = new int[4],
        Tribe4QuestDate = 0,
        Tribe4QuestState = 0,
        Tribe4QuestName = "",
        Zone049TypeState = new int[13],
        Zone049TypeStateTime = new int[13],
        Zone051TypeState = new int[6],
        Zone051TypeStateTime = new int[6],
        Zone053TypeState = new int[10],
        Zone053TypeStateTime = new int[10],
        Zone175TypeState = new int[32],
        TribeGuardState = new int[16],
        Zone194TypeState = 0,
        Zone297TypeState = new int[3],
        Zone297TypeBegin = new int[3],
        Zone038DTMValue = new int[4],
        TribeGeneralExperienceUpRatioInfo = new float[4],
        TribeItemDropUpRatioInfo = new float[4],
        TribeItemDropUpRatioForMyoungInfo = new float[4],
        TribeKillOtherTribeAddValueInfo = new int[4],
        TribeMasterCallAbility = new int[4],
        Zone267TypeState = new int[4],
        Zone241TypeState = new int[20],
        Zone270TypeState = new int[5],
        GuildBattle = 0,
        GuildName1 = "",
        GuildName2 = "",
        GuildName3 = ZeroStrings(3),
        GuildScore = new int[3],
        Zone088WinTribe = 0,
        FourGuildState = new int[4],
        FourGuildName = ZeroStrings(16),
        DecideChallengeFourGuildName = ZeroStrings(4),
        Zone200TypeState = 0,
        Zone5TypeState = new int[4],
        Zone54TypeState = 0,
        CountDrop8Cho = 0,
        TribeNokSanStone = new int[4],
        NokSanStoneState = new int[9],
        Zone319TypeState = new int[5],
        WaterRainHeavenState = 0,
        WaterRainHeavenStart = 0,
        ZoneFFATypeState = 0,
        Unk6 = new int[5],
        PopUpTypeState = new int[5],
        PopUpKillAvt = new int[5],
        PopUpKillMonster = new int[5]
    };

    public static readonly TribeInfo ZeroedTribeInfo = new()
    {
        TribeVoteName = ZeroStrings(40),
        TribeVoteLevel = new int[40],
        TribeVoteKillOtherTribe = new int[40],
        TribeVotePoint = new int[40],
        TribeMaster = ZeroStrings(4),
        TribeSubMaster = ZeroStrings(48),
        HoisundoName1 = ZeroStrings(20),
        HoisundoName2 = ZeroStrings(20),
        HoisundoName3 = ZeroStrings(20)
    };

    public static readonly BuffInfo ZeroedBuffInfo = new() { Buff = new int[70] };

    private static string[] ZeroStrings(int count)
    {
        var result = new string[count];
        for (var i = 0; i < count; i++)
            result[i] = "";

        return result;
    }
}
