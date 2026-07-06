namespace Fenrir.Application.Login.Domain;

/// <summary>
///     Thread-safe in-memory snapshot of the two server-wide inputs <see cref="LoginCapacityGate" /> reads.
///     Continuously refreshed by <c>Fenrir.Application.Login.Hosting.ServerQuotaRefreshHost</c> -- one
///     synchronous read at LoginServer startup, then a recurring re-read roughly once a second -- never bound
///     once from a config file and left fixed for the process lifetime. Holds no repository/database
///     dependency itself (that I/O belongs to the Hosting-layer refresh host); this type is purely the mutable
///     snapshot both that host (writer) and <c>LoginService</c> (reader) share.
/// </summary>
/// <remarks>
///     <see cref="CurrentPlayers" /> defaults to 0 rather than reproducing legacy's -1 "not yet populated"
///     sentinel (Server/ts25login/S07_MyGame01.cpp:10, mPresentPlayerNum = -1 in MyGame::Init): both values
///     compare identically ("less than any positive configured maximum") for the one narrow window this could
///     matter -- between startup's one-time max-only read and this process's first recurring tick -- so no
///     separate sentinel is needed here. See the behavior contract's "Current-count not yet populated
///     sentinel" edge case for the citation this rests on; not otherwise explored beyond that citation.
/// </remarks>
public sealed class LoginCapacityState
{
    private int _currentPlayers;
    private int _maxPlayers = -1;

    /// <summary>
    ///     -1 until the first successful read (startup or recurring) populates it; every gate evaluation in
    ///     production only ever runs after <c>ServerQuotaRefreshHost.InitializeAsync</c> has completed at boot,
    ///     so a real login attempt never observes -1.
    /// </summary>
    public int MaxPlayers => Volatile.Read(ref _maxPlayers);

    public int CurrentPlayers => Volatile.Read(ref _currentPlayers);

    public void SetMaxPlayers(int value)
    {
        Volatile.Write(ref _maxPlayers, value);
    }

    public void SetCurrentPlayers(int value)
    {
        Volatile.Write(ref _currentPlayers, value);
    }
}
