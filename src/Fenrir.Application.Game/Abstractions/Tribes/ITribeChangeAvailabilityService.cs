namespace Fenrir.Application.Game.Abstractions.Tribes;

public interface ITribeChangeAvailabilityService
{
    public bool IsChangeToTribeAllowed(byte toTribe);
}
