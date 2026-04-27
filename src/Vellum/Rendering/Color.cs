using System;
using System.Runtime.InteropServices;

namespace Vellum.Rendering;

/// <summary>
/// Straight RGBA color used by themes, widgets, and render vertices.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 4)]
public readonly struct Color : IEquatable<Color>
{
    /// <summary>Red channel.</summary>
    [FieldOffset(0)] public readonly byte R;
    /// <summary>Green channel.</summary>
    [FieldOffset(1)] public readonly byte G;
    /// <summary>Blue channel.</summary>
    [FieldOffset(2)] public readonly byte B;
    /// <summary>Alpha channel.</summary>
    [FieldOffset(3)] public readonly byte A;

    [FieldOffset(0)] private readonly uint packed;

    /// <summary>Creates a color from RGBA byte channels.</summary>
    public Color(byte r, byte g, byte b, byte a = 255)
    {
        packed = 0;

        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>Opaque white.</summary>
    public static readonly Color White = new(255, 255, 255);
    /// <summary>Opaque black.</summary>
    public static readonly Color Black = new(0, 0, 0);
    /// <summary>Opaque red.</summary>
    public static readonly Color Red = new(255, 0, 0);
    /// <summary>Opaque green.</summary>
    public static readonly Color Green = new(0, 255, 0);
    /// <summary>Opaque blue.</summary>
    public static readonly Color Blue = new(0, 0, 255);
    /// <summary>Fully transparent black.</summary>
    public static readonly Color Transparent = new(0, 0, 0, 0);

    /// <summary>Creates an opaque color from RGB byte channels.</summary>
    public static Color FromRgb(byte r, byte g, byte b)
    {
        return new Color(r, g, b);
    }

    /// <summary>Creates a color from RGBA byte channels.</summary>
    public static Color FromRgba(byte r, byte g, byte b, byte a)
    {
        return new Color(r, g, b, a);
    }

    /// <summary>Returns this color with a different alpha channel.</summary>
    public Color WithAlpha(byte a)
    {
        return new Color(R, G, B, a);
    }

    /// <inheritdoc />
    public bool Equals(Color other)
    {
        return packed == other.packed;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is Color other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return (int)packed;
    }

    /// <summary>Compares two colors for channel equality.</summary>
    public static bool operator ==(Color left, Color right)
    {
        return left.packed == right.packed;
    }

    /// <summary>Compares two colors for channel inequality.</summary>
    public static bool operator !=(Color left, Color right)
    {
        return left.packed != right.packed;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Color {{ R = {R}, G = {G}, B = {B}, A = {A} }}";
    }
}
