using Microsoft.Data.SqlClient;

namespace Fenrir.Application.Game.Services.Commerce;

public static class ProxyShopDeputyFailureClassifier
{
    public const int StaleListingSqlErrorNumber = 50272;

    public static bool IsStaleListingFailure(Exception ex)
    {
        return ex is SqlException { Number: StaleListingSqlErrorNumber };
    }
}
