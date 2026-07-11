using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{

        public ImmutableArray<int> CostumeExpireDate { get; set; } = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        public ImmutableArray<int> StellarCoreExpireDate { get; set; } = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
}
