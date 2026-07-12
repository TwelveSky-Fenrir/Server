using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Tests.World;

public class PortalProximityCatalogTests
{
    private const short SourceZone = 10;

    [Fact]
    public void TryGetPortals_EmptyCatalog_NeverHasAnyZone()
    {
        Assert.False(PortalProximityCatalog.Empty.TryGetPortals(SourceZone, out _));
    }
}
