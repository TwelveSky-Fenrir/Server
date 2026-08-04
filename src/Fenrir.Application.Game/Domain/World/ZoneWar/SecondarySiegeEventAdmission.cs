namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class SecondarySiegeEventAdmission(bool isEnabled)
{
    public bool IsEnabled { get; } = isEnabled;

    public bool Allows(int eventCode)
    {
        return IsEnabled || eventCode is < Zone051Zone053BroadcastResolver.Zone051RangeStart
            or > Zone051Zone053BroadcastResolver.Zone053RangeEnd;
    }
}
