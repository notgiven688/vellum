using System.Collections.Generic;
using System.Numerics;

namespace Vellum;

/// <summary>
/// Keyboard keys Vellum understands for navigation and text editing.
/// </summary>
public enum UiKey
{
    /// <summary>Left arrow key.</summary>
    Left,
    /// <summary>Right arrow key.</summary>
    Right,
    /// <summary>Up arrow key.</summary>
    Up,
    /// <summary>Down arrow key.</summary>
    Down,
    /// <summary>Home key.</summary>
    Home,
    /// <summary>End key.</summary>
    End,
    /// <summary>Tab key.</summary>
    Tab,
    /// <summary>Enter or return key.</summary>
    Enter,
    /// <summary>Escape key.</summary>
    Escape,
    /// <summary>Space key.</summary>
    Space,
    /// <summary>Backspace key.</summary>
    Backspace,
    /// <summary>Delete key.</summary>
    Delete,
    /// <summary>A key, used for common shortcut handling.</summary>
    A,
    /// <summary>C key, used for common shortcut handling.</summary>
    C,
    /// <summary>V key, used for common shortcut handling.</summary>
    V,
    /// <summary>X key, used for common shortcut handling.</summary>
    X
}

/// <summary>
/// Mouse buttons Vellum tracks for widget interaction.
/// </summary>
public enum UiMouseButton
{
    /// <summary>Primary mouse button.</summary>
    Left,
    /// <summary>Secondary mouse button.</summary>
    Right,
    /// <summary>Middle mouse button.</summary>
    Middle
}

/// <summary>
/// Per-frame input snapshot supplied by the host application.
/// </summary>
public readonly struct UiInputState
{
    private readonly string? _textInput;

    /// <summary>Text typed during this frame.</summary>
    public string TextInput => _textInput ?? string.Empty;
    /// <summary>Keys pressed during this frame.</summary>
    public IReadOnlySet<UiKey>? PressedKeys { get; }
    /// <summary>Mouse buttons currently held during this frame.</summary>
    public IReadOnlySet<UiMouseButton>? DownMouseButtons { get; }
    /// <summary>Mouse wheel delta for this frame.</summary>
    public Vector2 WheelDelta { get; }
    /// <summary>Monotonic timestamp for this input frame, in seconds.</summary>
    public double? TimeSeconds { get; }
    /// <summary>Whether shift is currently held.</summary>
    public bool Shift { get; }
    /// <summary>Whether control is currently held.</summary>
    public bool Ctrl { get; }
    /// <summary>Whether alt is currently held.</summary>
    public bool Alt { get; }
    /// <summary>Whether the platform meta key is currently held.</summary>
    public bool Meta { get; }
    /// <summary>Whether the platform primary shortcut modifier is held.</summary>
    public bool PrimaryModifier => Ctrl || Meta;

    /// <summary>Creates an input snapshot for one Vellum frame.</summary>
    public UiInputState(
        string? textInput = null,
        IReadOnlySet<UiKey>? pressedKeys = null,
        Vector2 wheelDelta = default,
        bool shift = false,
        bool ctrl = false,
        bool alt = false,
        bool meta = false,
        IReadOnlySet<UiMouseButton>? downMouseButtons = null,
        double? timeSeconds = null)
    {
        _textInput = textInput;
        PressedKeys = pressedKeys;
        DownMouseButtons = downMouseButtons;
        WheelDelta = wheelDelta;
        TimeSeconds = timeSeconds;
        Shift = shift;
        Ctrl = ctrl;
        Alt = alt;
        Meta = meta;
    }

    /// <summary>Returns whether <paramref name="key"/> was pressed during this frame.</summary>
    public bool IsPressed(UiKey key) => PressedKeys?.Contains(key) == true;
    /// <summary>Returns whether <paramref name="button"/> is currently held.</summary>
    public bool IsMouseDown(UiMouseButton button) => DownMouseButtons?.Contains(button) == true;
}
