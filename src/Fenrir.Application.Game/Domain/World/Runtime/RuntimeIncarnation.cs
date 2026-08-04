using System.Security.Cryptography;

namespace Fenrir.Application.Game.Domain.World.Runtime;

public readonly record struct RuntimeIncarnation
{
    private readonly Guid _value;

    private RuntimeIncarnation(Guid value)
    {
        _value = value;
    }

        public Guid Value => IsValid
        ? _value
        : throw new InvalidOperationException("A runtime incarnation must be generated before it is used.");

        public bool IsValid => _value != Guid.Empty;

        public static RuntimeIncarnation Create()
    {
        Span<byte> bytes = stackalloc byte[16];
        Guid value;

        do
        {
            RandomNumberGenerator.Fill(bytes);
            value = new Guid(bytes);
        }
        while (value == Guid.Empty);

        return new RuntimeIncarnation(value);
    }
}
