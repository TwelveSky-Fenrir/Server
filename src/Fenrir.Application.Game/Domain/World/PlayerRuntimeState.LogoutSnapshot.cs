namespace Fenrir.Application.Game.Domain.World;

public readonly record struct PlayerLogoutSnapshot(
    long FlushSequence,
    short MapId,
    int PosX,
    int PosY,
    int PosZ,
    int Life,
    int Mana);

public sealed partial class PlayerRuntimeState
{
    private readonly object _logoutSnapshotGate = new();

    private PlayerLogoutSnapshot? _logoutSnapshot;

    public PlayerLogoutSnapshot? LogoutSnapshot
    {
        get
        {
            lock (_logoutSnapshotGate)
            {
                return _logoutSnapshot;
            }
        }
    }

    public void CaptureLogoutSnapshot()
    {
        var snapshot = new PlayerLogoutSnapshot(
            FlushSequence,
            MapId,
            (int)PosX,
            (int)PosY,
            (int)PosZ,
            Life,
            Mana);

        lock (_logoutSnapshotGate)
        {
            _logoutSnapshot = snapshot;
        }
    }

    public void AcknowledgePersistedLogoutSnapshot(PlayerLogoutSnapshot snapshot)
    {
        lock (_logoutSnapshotGate)
        {
            if (_logoutSnapshot is { } current && current == snapshot)
                _logoutSnapshot = null;
        }
    }
}
