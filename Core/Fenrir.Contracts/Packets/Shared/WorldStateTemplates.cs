namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     Wire-zero templates for the world-state structs embedded in ZC_BROADCAST_WORLD_INFO (<see cref="WorldInfo" />
///     + <see cref="TribeInfo" />) and ZC_REGISTER_AVATAR_RECV (<see cref="BuffInfo" />) — both packets are sent by
///     GameServer only. Guild wars, tribe votes, hoisundo elections, and zone-event state are entirely
///     M1-out-of-scope (no gameplay/data migration, M1 plan), so every field starts at its wire-zero value; there
///     is no persisted source to project from yet.
/// </summary>
public static class WorldStateTemplates
{
    /// <summary>
    ///     Every <see cref="WorldInfo" /> field at its wire-zero value: 0 for scalars, "" for <c>[FixedString]</c>
    ///     (the codec zero-fills up to the declared length itself — never hand-pad), a same-length-N array of
    ///     zeros for <c>[FixedArray]</c> int/float arrays (N must match this exact property's attribute, not a
    ///     neighbor's), and a same-length-N array of "" for <c>[FixedArray] [FixedString]</c> string rows (never a
    ///     bare <c>new string[N]</c>, which would leave <see langword="null" /> entries the codec cannot write).
    /// </summary>
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

    /// <summary>
    ///     Every <see cref="TribeInfo" /> field at its wire-zero value — same conventions as
    ///     <see cref="ZeroedWorldInfo" />.
    /// </summary>
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

    /// <summary>Every <see cref="BuffInfo" /> field at its wire-zero value.</summary>
    public static readonly BuffInfo ZeroedBuffInfo = new() { Buff = new int[70] };

    // Enumerable.Repeat(...).ToArray() would also work but allocates a LINQ iterator per call for what is really
    // just a fixed-size fill; a plain loop keeps this on the same footing as the int[] literals above.
    private static string[] ZeroStrings(int count)
    {
        var result = new string[count];
        for (var i = 0; i < count; i++)
            result[i] = "";

        return result;
    }
}
