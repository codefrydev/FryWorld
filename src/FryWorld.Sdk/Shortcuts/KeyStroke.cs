using System;
using Avalonia.Input;

namespace FryWorld.Sdk.Shortcuts;

/// <summary>
/// Represents a single key combination comprising a key and optional modifiers.
/// </summary>
public readonly record struct KeyStroke(Key Key, KeyModifiers Modifiers = KeyModifiers.None)
{
    /// <summary>
    /// Checks if this stroke matches the specified Avalonia <see cref="KeyEventArgs"/>.
    /// </summary>
    /// <param name="e">The key event arguments to evaluate.</param>
    /// <returns>True if the key and modifiers match exactly.</returns>
    public bool Matches(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.Key != Key)
            return false;

        // Strip non-standard or lock flags (e.g. NumLock, CapsLock) if any, comparing only Control, Alt, Shift, Meta
        var relevantModifiers = e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta);
        var strokeModifiers = Modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta);

        return relevantModifiers == strokeModifiers;
    }

    /// <summary>
    /// Formats this stroke as a human-readable string suitable for display in the current platform's UI.
    /// </summary>
    /// <returns>Display string, e.g. "⌘S" on macOS or "Ctrl+S" on Windows/Linux.</returns>
    public string ToDisplayString()
    {
        bool isMac = OperatingSystem.IsMacOS();
        var parts = new System.Collections.Generic.List<string>(4);

        if (isMac)
        {
            if (Modifiers.HasFlag(KeyModifiers.Control)) parts.Add("⌃");
            if (Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("⌥");
            if (Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("⇧");
            if (Modifiers.HasFlag(KeyModifiers.Meta)) parts.Add("⌘");
            parts.Add(FormatKey(Key, isMac));
            return string.Concat(parts);
        }
        else
        {
            if (Modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Win");
            parts.Add(FormatKey(Key, isMac));
            return string.Join("+", parts);
        }
    }

    private static string FormatKey(Key key, bool isMac) => key switch
    {
        Key.Delete => "Delete",
        Key.Back => isMac ? "⌫" : "Backspace",
        Key.Escape => "Esc",
        Key.Return => isMac ? "↩" : "Enter",
        Key.Tab => "Tab",
        Key.Space => "Space",
        Key.OemPlus or Key.Add => "+",
        Key.OemMinus or Key.Subtract => "-",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.D0 => "0",
        Key.D1 => "1",
        Key.D2 => "2",
        Key.D3 => "3",
        Key.D4 => "4",
        Key.D5 => "5",
        Key.D6 => "6",
        Key.D7 => "7",
        Key.D8 => "8",
        Key.D9 => "9",
        _ => key.ToString()
    };
}
