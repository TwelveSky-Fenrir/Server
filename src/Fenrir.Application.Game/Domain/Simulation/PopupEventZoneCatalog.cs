using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Simulation;

public enum PopupEventType : byte
{
    RegularWar = 0,

    YanggokPvp = 1,

    MonsterPve = 2,

    InvasionPvp = 3,

    RuinsPvp = 4
}

public static class PopupEventZoneCatalog
{
    public const int WarKillThreshold = 10;

    public const int YanggokKillThreshold = 10;

    public const int InvasionKillThreshold = 5;

    public const int MonsterKillThreshold = 400;

    public const short RegularWarMapId = 160;

    private static readonly FrozenSet<short> RegularWarMaps = new[] { RegularWarMapId }.ToFrozenSet();

    private static readonly FrozenSet<short> YanggokMaps = new short[] { 38 }.ToFrozenSet();

    private static readonly FrozenSet<short> MonsterMaps = new short[]
    {
        104, 105, 106, 107, 108, 109, 128, 129, 132, 133, 136, 137, 168, 169, 173, 174, 145
    }.ToFrozenSet();

    private static readonly FrozenSet<short> InvasionMaps = new short[] { 1, 6, 11, 140 }.ToFrozenSet();

    private static readonly FrozenSet<short> RuinsMaps = new short[] { 268 }.ToFrozenSet();

    public static bool TryResolvePvpType(short mapId, out PopupEventType type)
    {
        if (RegularWarMaps.Contains(mapId))
        {
            type = PopupEventType.RegularWar;
            return true;
        }

        if (YanggokMaps.Contains(mapId))
        {
            type = PopupEventType.YanggokPvp;
            return true;
        }

        if (InvasionMaps.Contains(mapId))
        {
            type = PopupEventType.InvasionPvp;
            return true;
        }

        if (RuinsMaps.Contains(mapId))
        {
            type = PopupEventType.RuinsPvp;
            return true;
        }

        type = default;
        return false;
    }

    public static bool IsMonsterPopupMap(short mapId)
    {
        return MonsterMaps.Contains(mapId);
    }

    public static int KillThreshold(PopupEventType type, short mapId)
    {
        return type switch
        {
            PopupEventType.RegularWar or PopupEventType.RuinsPvp => WarKillThreshold,
            PopupEventType.YanggokPvp => YanggokKillThreshold,
            PopupEventType.InvasionPvp => InvasionKillThreshold,
            PopupEventType.MonsterPve => MonsterKillThreshold,
            _ => WarKillThreshold
        };
    }

    public static bool UsesWarCounter(PopupEventType type)
    {
        return type is PopupEventType.RegularWar or PopupEventType.RuinsPvp;
    }
}

public sealed class PopupEventState
{
    private readonly int[] _countdowns = new int[5];
    private readonly int[] _enabled = new int[5];

    private readonly int[] _pvmRequirements = new int[5];

    private readonly int[] _pvpRequirements = new int[5];

    public bool IsEnabled(PopupEventType type)
    {
        return Volatile.Read(ref _enabled[(int)type]) != 0;
    }

    public void SetEnabled(PopupEventType type, bool enabled)
    {
        Volatile.Write(ref _enabled[(int)type], enabled ? 1 : 0);
    }

    public void ApplyState(PopupEventType type, bool enabled, int pvpRequirement, int pvmRequirement)
    {
        var index = (int)type;
        Volatile.Write(ref _pvpRequirements[index], pvpRequirement);
        Volatile.Write(ref _pvmRequirements[index], pvmRequirement);
        Volatile.Write(ref _enabled[index], enabled ? 1 : 0);
    }

    public void SetCountdown(PopupEventType type, int remainingMinutes)
    {
        Volatile.Write(ref _countdowns[(int)type], remainingMinutes);
    }

    public PopupEventWorldSnapshot GetSnapshot(PopupEventType type)
    {
        var index = (int)type;
        return new PopupEventWorldSnapshot(
            Volatile.Read(ref _enabled[index]) != 0,
            Volatile.Read(ref _pvpRequirements[index]),
            Volatile.Read(ref _pvmRequirements[index]),
            Volatile.Read(ref _countdowns[index]));
    }
}

public readonly record struct PopupEventWorldSnapshot(
    bool Enabled,
    int PvpRequirement,
    int PvmRequirement,
    int CountdownMinutes);
