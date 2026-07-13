namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public interface IZone195NokSanBroadcaster
{
    public void AnnounceChallengerAppeared(byte challengerTribe, string challengerName);

    public void AnnounceCaptureCancelled(short serverNumber);

    public void AnnounceCountdown(int remainingTime, short serverNumber);

    public void AnnounceCaptureSucceeded(byte winningTribe, short serverNumber, string capturerName);

    public void AnnounceNokSanState(byte owningTribe, short serverNumber, Zone195NokSanStateSnapshot snapshot);
}
