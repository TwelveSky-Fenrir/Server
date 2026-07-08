using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>
///     Business logic for CZ_DEMAND_BLOOD_MARK_SEND (opcode 140), extracted from
///     <see cref="GetBloodMarkCatalogHandler" />.
/// </summary>
public interface IGetBloodMarkCatalogService
{
    public BloodShop GetCatalog();
}
