namespace Fenrir.Application.Login.Domain;

/// <summary>Outcome of <see cref="LoginCapacityGate.Evaluate" />.</summary>
public enum LoginCapacityOutcome
{
    Allowed,
    Maintenance,
    ServerFull
}

/// <summary>
///     Pure evaluation of the two earliest CL_LOGIN_SEND gates: maintenance lockdown and the server-full quota
///     check. Both depend solely on server-wide state (never a field of the incoming packet) and both run
///     ahead of every other check in <c>LoginService.LoginAsync</c> -- see that type's own remarks for why the
///     Fenrir-only IP rate limiter still sits ahead of these two despite that ordering.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:149-154 (maintenance -- <c>mMaxPlayerNum == 0</c>, tResult=1)
///     et :155-160 (quota serveur plein -- <c>mPresentPlayerNum &gt;= mMaxPlayerNum</c>, tResult=3), les deux
///     évaluées avant le contrôle de version (:161-166). Le maximum configuré négatif n'est pas géré
///     explicitement côté legacy (branche non observée/exercée) : ici, il retombe simplement dans la
///     comparaison de quota ci-dessous, sans cas particulier.
/// </remarks>
public static class LoginCapacityGate
{
    /// <summary>
    ///     <paramref name="maxPlayers" /> == 0 is maintenance mode, not "zero capacity", and is checked first --
    ///     a configured maximum of zero always yields <see cref="LoginCapacityOutcome.Maintenance" /> even when
    ///     <paramref name="currentPlayers" /> would otherwise also trip the quota comparison. The quota
    ///     comparison itself rejects at "reached" (<paramref name="currentPlayers" /> == <paramref name="maxPlayers" />),
    ///     not only "exceeded".
    /// </summary>
    public static LoginCapacityOutcome Evaluate(int maxPlayers, int currentPlayers)
    {
        if (maxPlayers == 0)
            return LoginCapacityOutcome.Maintenance;

        return currentPlayers >= maxPlayers ? LoginCapacityOutcome.ServerFull : LoginCapacityOutcome.Allowed;
    }
}
