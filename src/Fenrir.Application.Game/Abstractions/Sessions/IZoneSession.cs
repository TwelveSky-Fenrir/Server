using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.Sessions;

public enum GmCommandTier : short
{
    Basic = 1,
    Elevated = 10,
    Admin = 100
}

public interface IZoneActor;

public interface IZoneSession : IPacketSession
{
    public ZoneSessionState State { get; }

    public int? AccountId { get; }

    public int? CharacterId { get; }

    public Guid? AccountSessionToken { get; }

    public short AccountGrade { get; }

    public short? TargetMapId { get; }

    public bool IsGm { get; }

    public IZoneActor? CurrentZone { get; set; }

    public bool IsZoneTransferPending { get; }

    public bool MeetsGmTier(GmCommandTier tier);

    public void MarkTicketConsumed(int accountId, int characterId, Guid? sessionToken = null, short accountGrade = 0,
        short targetMapId = 0);

    public void MarkRegistering();

    public void MarkInWorld();

    public void MarkZoneTransferPending();

    public void ClearZoneTransferPending();
}
