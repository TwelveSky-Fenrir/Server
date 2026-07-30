namespace Fenrir.Security.Abstractions;

public static class CenterLinkAuth
{
    public const int NonceSize = 32;

    public const int MacSize = 32;

    public const int MaxContextLength = 256;

    public const int MinSecretLength = 16;
}
