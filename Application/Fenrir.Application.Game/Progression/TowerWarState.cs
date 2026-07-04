namespace Fenrir.Application.Game.Progression;

/// <summary>
///     Process-wide mirror of the legacy tower-war state CZ_CHUGSOUNG_WAR_UP_SEND reads/writes. The legacy
///     ran one zone-server process per tower, splitting this across two places: the shared-memory
///     <c>TOWER_INFO.mState1Tower[12]</c> array (packed level*100+type, decoded by
///     <c>MyGame::GetTowerState</c>/<c>GetTowerType</c>) and each zone-server's own local
///     <c>mTowerValid</c>/<c>mTowerState</c> scalars for whichever single tower it owned. Fenrir runs every
///     zone in one process, so both collapse onto one per-tower record here; every state the legacy could
///     reach produces the same decoded level/type/valid values either way.
///     <para>
///         Nothing yet flips a tower into a siege (no CreateTower/ProcessForTower guardian-monster tick
///         exists in Fenrir), so every tower starts, and stays, at PackedState=0/Valid=false --
///         <see cref="SetTowerState" /> is real production API, just not called by anything yet.
///     </para>
/// </summary>
public sealed class TowerWarState
{
    public const int TowerCount = 12;
    private readonly Lock _lock = new();

    private readonly int[] _packedState = new int[TowerCount];
    private readonly bool[] _valid = new bool[TowerCount];

    public int GetPackedState(int towerIndex)
    {
        lock (_lock)
        {
            return _packedState[towerIndex];
        }
    }

    public bool IsValid(int towerIndex)
    {
        lock (_lock)
        {
            return _valid[towerIndex];
        }
    }

    /// <summary>
    ///     Legacy <c>MyGame::GetTowerState</c> -- 0 if untouched (packed &lt; 1), else the level digits (packed/100):
    ///     2/4/6/8.
    /// </summary>
    public static int DecodeLevel(int packedState)
    {
        return packedState < 1 ? 0 : packedState / 100;
    }

    /// <summary>Legacy <c>MyGame::GetTowerType</c> -- 0 if untouched, else the type digits (packed%100): 1/2/3.</summary>
    public static int DecodeType(int packedState)
    {
        return packedState < 1 ? 0 : packedState % 100;
    }

    /// <summary>Arms a tower for upgrade -- reserved for the not-yet-built siege-trigger subsystem.</summary>
    public void SetTowerState(int towerIndex, int packedState, bool valid)
    {
        lock (_lock)
        {
            _packedState[towerIndex] = packedState;
            _valid[towerIndex] = valid;
        }
    }

    /// <summary>
    ///     CZ_CHUGSOUNG_WAR_UP_SEND's own success path only ever clears <c>mTowerValid</c> --
    ///     <c>mState1Tower</c> itself is left untouched (only ProcessForTower's tick, not built in Fenrir,
    ///     ever writes it), so a resubmission is blocked but the decoded level/type do not advance yet.
    /// </summary>
    public void MarkUpgradeSubmitted(int towerIndex)
    {
        lock (_lock)
        {
            _valid[towerIndex] = false;
        }
    }
}
