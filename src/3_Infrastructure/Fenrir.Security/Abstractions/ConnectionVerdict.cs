namespace Fenrir.Security.Abstractions;

/// <summary>Verdict d'admission d'une connexion entrante, évalué <b>au socket accept</b>.</summary>
public enum ConnectionVerdict : byte
{
    /// <summary>Connexion admise (poursuite du handshake).</summary>
    Allow = 0,

    /// <summary>Connexion refusée (IP bloquée / flood) — socket fermé immédiatement.</summary>
    Block = 1,
}
