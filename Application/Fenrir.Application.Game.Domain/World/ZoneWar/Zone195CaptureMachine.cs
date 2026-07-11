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
