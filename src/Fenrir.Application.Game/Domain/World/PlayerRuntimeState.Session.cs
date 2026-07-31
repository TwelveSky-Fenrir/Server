namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    public DateTime? LastSentHeartbeat { get; set; }

    public uint? PrevSentHeartbeat { get; set; }

    public DateTime? ConnectTime { get; set; }

    public int AutoTimeHack { get; set; }

    public bool IsMuted { get; set; }
}
