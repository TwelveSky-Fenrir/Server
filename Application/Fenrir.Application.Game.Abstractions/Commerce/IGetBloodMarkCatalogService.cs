using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public interface IGetBloodMarkCatalogService
{
    public BloodShop GetCatalog();
}
