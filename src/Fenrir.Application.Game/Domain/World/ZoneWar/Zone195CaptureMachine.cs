namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public enum Zone195CapturePhase : byte
{
    IdleSearching = 0,

    Settle = 1,

    Countdown = 2,

    Commit = 3
}

public sealed class Zone195CaptureMachine
{
    public const int NoCapturer = -1;

    public Zone195CapturePhase Phase { get; set; } = Zone195CapturePhase.IdleSearching;

    public int CapturerCharacterId { get; set; } = NoCapturer;

    public byte CapturerTribe { get; set; }

    public string CapturerName { get; set; } = string.Empty;

    public int RemainingTime { get; set; }

    public int PhaseAccumulatorTicks { get; set; }

        public Zone195NokSanCaptureSnapshot Snapshot(short mapId)
    {
        return new Zone195NokSanCaptureSnapshot(mapId, Phase, CapturerCharacterId, CapturerTribe, CapturerName,
            RemainingTime, PhaseAccumulatorTicks);
    }

    public void Restore(in Zone195NokSanCaptureSnapshot snapshot)
    {
        Phase = snapshot.Phase;
        CapturerCharacterId = snapshot.CapturerCharacterId;
        CapturerTribe = snapshot.CapturerTribe;
        CapturerName = snapshot.CapturerName;
        RemainingTime = snapshot.RemainingTime;
        PhaseAccumulatorTicks = snapshot.PhaseAccumulatorTicks;
    }

    public void ResetToIdle()
    {
        Phase = Zone195CapturePhase.IdleSearching;
        CapturerCharacterId = NoCapturer;
        CapturerTribe = 0;
        CapturerName = string.Empty;
        RemainingTime = 0;
        PhaseAccumulatorTicks = 0;
    }
}

public readonly record struct Zone195NokSanCaptureSnapshot(
    short MapId,
    Zone195CapturePhase Phase,
    int CapturerCharacterId,
    byte CapturerTribe,
    string CapturerName,
    int RemainingTime,
    int PhaseAccumulatorTicks)
{
    public bool HasExpectedShape =>
        Zone195NokSanSiteCatalog.IsActiveMapId(MapId)
        && Phase is Zone195CapturePhase.IdleSearching or Zone195CapturePhase.Settle or Zone195CapturePhase.Countdown
        && CapturerCharacterId >= Zone195CaptureMachine.NoCapturer
        && CapturerTribe < Zone195NokSanState.TribeCount
        && CapturerName is not null
        && CapturerName.Length <= Zone195NokSanState.MaximumAvatarNameLength
        && RemainingTime >= 0
        && PhaseAccumulatorTicks >= 0
        && (Phase != Zone195CapturePhase.IdleSearching ||
            (CapturerCharacterId == Zone195CaptureMachine.NoCapturer && CapturerTribe == 0 &&
             CapturerName.Length == 0 && RemainingTime == 0 && PhaseAccumulatorTicks == 0))
        && (Phase == Zone195CapturePhase.IdleSearching ||
            (CapturerCharacterId > 0 && CapturerName.Length > 0));
}
