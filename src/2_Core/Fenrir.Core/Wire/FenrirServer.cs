namespace Fenrir.Core.Wire;

/// <summary>
/// Serveur propriétaire d'un opcode. Un même octet d'opcode est réutilisé entre serveurs/sens (legacy) : la clé
/// de dispatch est (<see cref="FenrirServer"/>, <see cref="FenrirDirection"/>, opcode). Jamais sérialisé dans
/// l'en-tête (l'en-tête ne porte que l'octet d'opcode). <c>Center</c> = ajout Fenrir (nouveau CenterServer TCP).
/// </summary>
public enum FenrirServer : byte
{
    Login = 0,
    Zone = 1,
    Center = 2
}
