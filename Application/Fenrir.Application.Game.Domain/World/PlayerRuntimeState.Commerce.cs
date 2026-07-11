namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{

        public const int CashCatalogVersionUnknown = -1;

        public int KnownCashCatalogVersion { get; set; } = CashCatalogVersionUnknown;

        public DateTime? LastCashItemPurchaseAtUtc { get; set; }
}
