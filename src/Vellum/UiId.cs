namespace Vellum;

/// <summary>
/// Strongly-typed identifier accepted by widget <c>id:</c> parameters, explicit-ID
/// containers, and <see cref="Ui.RequestFocus(UiWidgetKind, UiId)"/>. Construct one implicitly from
/// <see cref="string"/>, <see cref="int"/>, <see cref="long"/>, <see cref="ulong"/>, or
/// <see cref="System.Guid"/>, or call the <c>From*</c> factories explicitly.
/// </summary>
/// <remarks>
/// Use stable data identity for <see cref="UiId"/> values. If an ID is derived
/// from editable text, changing that text creates a new widget identity and
/// Vellum will not carry over UI state such as focus, scroll positions, or
/// selected tabs.
/// </remarks>
public readonly struct UiId : System.IEquatable<UiId>
{
    internal readonly int Hash;
    private readonly bool _specified;

    internal bool IsSpecified => _specified;

    private UiId(int hash)
    {
        Hash = hash;
        _specified = true;
    }

    /// <summary>Creates a <see cref="UiId"/> from a character span.</summary>
    public static UiId FromString(System.ReadOnlySpan<char> value) => new(HashString(value));

    /// <summary>Creates a <see cref="UiId"/> from a string.</summary>
    public static UiId FromString(string value)
    {
        System.ArgumentNullException.ThrowIfNull(value);
        return FromString(value.AsSpan());
    }

    /// <summary>Creates a <see cref="UiId"/> from a 32-bit signed integer.</summary>
    public static UiId FromInt32(int value) => new(HashInt(value));

    /// <summary>Creates a <see cref="UiId"/> from a 64-bit signed integer.</summary>
    public static UiId FromInt64(long value) => new(HashLong(value));

    /// <summary>Creates a <see cref="UiId"/> from a 64-bit unsigned integer.</summary>
    public static UiId FromUInt64(ulong value) => new(HashLong(unchecked((long)value)));

    /// <summary>Creates a <see cref="UiId"/> from a <see cref="System.Guid"/>.</summary>
    public static UiId FromGuid(System.Guid value) => new(HashGuid(value));

    /// <summary>Implicit conversion from <see cref="string"/>.</summary>
    public static implicit operator UiId(string? value) => value is null ? default : FromString(value);

    /// <summary>Implicit conversion from <see cref="int"/>.</summary>
    public static implicit operator UiId(int value) => FromInt32(value);

    /// <summary>Implicit conversion from <see cref="long"/>.</summary>
    public static implicit operator UiId(long value) => FromInt64(value);

    /// <summary>Implicit conversion from <see cref="ulong"/>.</summary>
    public static implicit operator UiId(ulong value) => FromUInt64(value);

    /// <summary>Implicit conversion from <see cref="System.Guid"/>.</summary>
    public static implicit operator UiId(System.Guid value) => FromGuid(value);

    /// <inheritdoc />
    public bool Equals(UiId other) => _specified == other._specified && Hash == other.Hash;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is UiId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _specified ? Hash : 0;

    /// <summary>Equality operator.</summary>
    public static bool operator ==(UiId left, UiId right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(UiId left, UiId right) => !left.Equals(right);

    internal static int HashString(System.ReadOnlySpan<char> text)
    {
        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619;
            }

            return (int)hash;
        }
    }

    internal static int HashInt(int value)
    {
        unchecked
        {
            uint hash = 2166136261;
            hash ^= (uint)value;
            hash *= 16777619;
            return (int)hash;
        }
    }

    internal static int HashLong(long value)
    {
        unchecked
        {
            uint hash = 2166136261;
            ulong bits = (ulong)value;
            hash ^= (uint)bits;
            hash *= 16777619;
            hash ^= (uint)(bits >> 32);
            hash *= 16777619;
            return (int)hash;
        }
    }

    internal static int HashGuid(System.Guid value)
    {
        System.Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes);

        unchecked
        {
            uint hash = 2166136261;
            foreach (byte b in bytes)
            {
                hash ^= b;
                hash *= 16777619;
            }

            return (int)hash;
        }
    }
}
