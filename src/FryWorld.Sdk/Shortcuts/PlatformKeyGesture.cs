using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;

namespace FryWorld.Sdk.Shortcuts;

/// <summary>
/// Cross-platform parser, formatter, and matcher for keyboard gestures and multi-key chord sequences.
/// Supports standard modifier aliases such as "Mod", "CmdOrCtrl", "Cmd", "Ctrl", "Alt", and "Shift".
/// </summary>
public sealed class PlatformKeyGesture
{
    private readonly KeyStroke[] _strokes;

    /// <summary>
    /// Gets the sequence of strokes that constitute this gesture.
    /// A standard shortcut has 1 stroke; a chord sequence has 2 or more strokes.
    /// </summary>
    public IReadOnlyList<KeyStroke> Strokes => _strokes;

    /// <summary>
    /// Whether this gesture is a multi-key chord sequence (e.g. "Mod+K, Mod+C").
    /// </summary>
    public bool IsChord => _strokes.Length > 1;

    /// <summary>
    /// The primary stroke of the gesture.
    /// </summary>
    public KeyStroke PrimaryStroke => _strokes.Length > 0 ? _strokes[0] : default;

    /// <summary>
    /// Initializes a new instance of <see cref="PlatformKeyGesture"/> with the specified strokes.
    /// </summary>
    /// <param name="strokes">The strokes forming the gesture.</param>
    public PlatformKeyGesture(IEnumerable<KeyStroke> strokes)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        _strokes = strokes.ToArray();
        if (_strokes.Length == 0)
        {
            throw new ArgumentException("A key gesture must have at least one stroke.", nameof(strokes));
        }
    }

    /// <summary>
    /// Initializes a new instance of <see cref="PlatformKeyGesture"/> for a single stroke.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">Optional modifier flags.</param>
    public PlatformKeyGesture(Key key, KeyModifiers modifiers = KeyModifiers.None)
        : this([new KeyStroke(key, modifiers)])
    {
    }

    /// <summary>
    /// Parses a gesture string into a <see cref="PlatformKeyGesture"/>.
    /// Supports chords delimited by commas or spaces, and modifier tokens like "Mod", "Cmd", "Ctrl", "Alt", "Shift".
    /// </summary>
    /// <param name="gestureString">The gesture text (e.g. "Mod+S", "Cmd+Shift+P", "Mod+K, Mod+C").</param>
    /// <returns>A parsed <see cref="PlatformKeyGesture"/>.</returns>
    /// <exception cref="FormatException">The gesture string could not be parsed.</exception>
    public static PlatformKeyGesture Parse(string gestureString)
    {
        if (TryParse(gestureString, out var result) && result != null)
        {
            return result;
        }

        throw new FormatException($"Invalid key gesture string: '{gestureString}'.");
    }

    /// <summary>
    /// Attempts to parse a gesture string into a <see cref="PlatformKeyGesture"/>.
    /// </summary>
    /// <param name="gestureString">The gesture text to parse.</param>
    /// <param name="gesture">When this method returns, contains the parsed gesture if successful, or null on failure.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string? gestureString, out PlatformKeyGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(gestureString))
            return false;

        var rawStrokes = SplitChordStrokes(gestureString.Trim());
        if (rawStrokes.Count == 0)
            return false;

        var strokes = new List<KeyStroke>(rawStrokes.Count);
        foreach (var rawStroke in rawStrokes)
        {
            if (!TryParseStroke(rawStroke, out var stroke))
            {
                return false;
            }
            strokes.Add(stroke);
        }

        if (strokes.Count == 0)
            return false;

        gesture = new PlatformKeyGesture(strokes);
        return true;
    }

    private static List<string> SplitChordStrokes(string gestureString)
    {
        var strokes = new List<string>();
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < gestureString.Length; i++)
        {
            char c = gestureString[i];

            if (c == ',')
            {
                // If comma is preceded by '+', it is the key in e.g. "Mod+,", not a chord delimiter
                bool isPrecededByPlus = i > 0 && gestureString[i - 1] == '+';
                bool isSoleChar = gestureString.Trim().Length == 1;

                if (isPrecededByPlus || isSoleChar)
                {
                    current.Append(c);
                    continue;
                }

                // Otherwise it's a chord delimiter
                var stroke = current.ToString().Trim();
                if (!string.IsNullOrEmpty(stroke))
                {
                    strokes.Add(stroke);
                    current.Clear();
                }

                // Skip any whitespace immediately following the comma
                while (i + 1 < gestureString.Length && char.IsWhiteSpace(gestureString[i + 1]))
                {
                    i++;
                }
            }
            else if (char.IsWhiteSpace(c))
            {
                var stroke = current.ToString().Trim();
                if (!string.IsNullOrEmpty(stroke) && !stroke.EndsWith('+'))
                {
                    strokes.Add(stroke);
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        var lastStroke = current.ToString().Trim();
        if (!string.IsNullOrEmpty(lastStroke))
        {
            strokes.Add(lastStroke);
        }

        return strokes;
    }

    /// <summary>
    /// Checks if a single-stroke gesture matches the specified Avalonia <see cref="KeyEventArgs"/>.
    /// For multi-stroke chords, use the chord matching state machine in <see cref="IShortcutManager"/>.
    /// </summary>
    /// <param name="e">The key event to check.</param>
    /// <returns>True if the primary stroke matches the key event.</returns>
    public bool Matches(KeyEventArgs e)
    {
        return !IsChord && _strokes[0].Matches(e);
    }

    /// <summary>
    /// Formats this gesture for display on the current operating system.
    /// </summary>
    /// <returns>A localized, platform-adapted display string (e.g. "⌘S" or "Ctrl+S").</returns>
    public string ToDisplayString()
    {
        if (_strokes.Length == 1)
        {
            return _strokes[0].ToDisplayString();
        }

        return string.Join(" ", _strokes.Select(s => s.ToDisplayString()));
    }

    /// <inheritdoc />
    public override string ToString() => ToDisplayString();

    private static bool TryParseStroke(string strokeText, out KeyStroke stroke)
    {
        stroke = default;
        if (string.IsNullOrWhiteSpace(strokeText))
            return false;

        strokeText = strokeText.Trim();

        // Special case: single character token
        if (strokeText.Length == 1)
        {
            if (TryParseKey(strokeText, out var singleKey))
            {
                stroke = new KeyStroke(singleKey, KeyModifiers.None);
                return true;
            }
            return false;
        }

        string keyToken;
        string modPrefix;

        if (strokeText.EndsWith("++"))
        {
            keyToken = "+";
            modPrefix = strokeText[..^2];
        }
        else
        {
            int lastPlus = strokeText.LastIndexOf('+');
            if (lastPlus >= 0 && lastPlus < strokeText.Length - 1)
            {
                modPrefix = strokeText[..lastPlus];
                keyToken = strokeText[(lastPlus + 1)..].Trim();
            }
            else
            {
                modPrefix = string.Empty;
                keyToken = strokeText;
            }
        }

        bool isMac = OperatingSystem.IsMacOS();
        KeyModifiers modifiers = KeyModifiers.None;

        if (!string.IsNullOrWhiteSpace(modPrefix))
        {
            var tokens = modPrefix.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var modToken in tokens)
            {
                if (modToken.Equals("Mod", StringComparison.OrdinalIgnoreCase) ||
                    modToken.Equals("CmdOrCtrl", StringComparison.OrdinalIgnoreCase) ||
                    modToken.Equals("PrimaryMod", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= isMac ? KeyModifiers.Meta : KeyModifiers.Control;
                }
                else if (modToken.Equals("Cmd", StringComparison.OrdinalIgnoreCase) ||
                         modToken.Equals("Command", StringComparison.OrdinalIgnoreCase) ||
                         modToken.Equals("Meta", StringComparison.OrdinalIgnoreCase) ||
                         modToken.Equals("⌘", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= KeyModifiers.Meta;
                }
                else if (modToken.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                         modToken.Equals("Control", StringComparison.OrdinalIgnoreCase) ||
                         modToken.Equals("⌃", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= KeyModifiers.Control;
                }
                else if (modToken.Equals("Alt", StringComparison.OrdinalIgnoreCase) ||
                         modToken.Equals("Option", StringComparison.OrdinalIgnoreCase) ||
                         modToken.Equals("⌥", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= KeyModifiers.Alt;
                }
                else if (modToken.Equals("Shift", StringComparison.OrdinalIgnoreCase) ||
                         modToken.Equals("⇧", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= KeyModifiers.Shift;
                }
                else
                {
                    return false;
                }
            }
        }

        if (!TryParseKey(keyToken, out var key))
        {
            return false;
        }

        stroke = new KeyStroke(key, modifiers);
        return true;
    }

    private static bool TryParseKey(string keyToken, out Key key)
    {
        key = Key.None;
        if (string.IsNullOrWhiteSpace(keyToken))
            return false;

        // Handle single characters and symbols
        if (keyToken.Length == 1)
        {
            char c = char.ToUpperInvariant(keyToken[0]);
            if (c >= 'A' && c <= 'Z')
            {
                key = (Key)((int)Key.A + (c - 'A'));
                return true;
            }
            if (c >= '0' && c <= '9')
            {
                key = (Key)((int)Key.D0 + (c - '0'));
                return true;
            }

            key = c switch
            {
                ',' => Key.OemComma,
                '.' => Key.OemPeriod,
                '/' => Key.Oem2,
                ';' => Key.Oem1,
                '\'' => Key.Oem7,
                '[' => Key.Oem4,
                ']' => Key.Oem6,
                '\\' => Key.Oem5,
                '-' => Key.OemMinus,
                '=' or '+' => Key.OemPlus,
                '`' => Key.OemTilde,
                _ => Key.None
            };

            return key != Key.None;
        }

        // Common key aliases
        if (keyToken.Equals("Comma", StringComparison.OrdinalIgnoreCase))
        {
            key = Key.OemComma;
            return true;
        }
        if (keyToken.Equals("Period", StringComparison.OrdinalIgnoreCase) || keyToken.Equals("Dot", StringComparison.OrdinalIgnoreCase))
        {
            key = Key.OemPeriod;
            return true;
        }
        if (keyToken.Equals("Plus", StringComparison.OrdinalIgnoreCase))
        {
            key = Key.OemPlus;
            return true;
        }
        if (keyToken.Equals("Minus", StringComparison.OrdinalIgnoreCase))
        {
            key = Key.OemMinus;
            return true;
        }
        if (keyToken.Equals("Esc", StringComparison.OrdinalIgnoreCase) || keyToken.Equals("Escape", StringComparison.OrdinalIgnoreCase))
        {
            key = Key.Escape;
            return true;
        }
        if (keyToken.Equals("Enter", StringComparison.OrdinalIgnoreCase) || keyToken.Equals("Return", StringComparison.OrdinalIgnoreCase))
        {
            key = Key.Return;
            return true;
        }
        if (keyToken.Equals("Backspace", StringComparison.OrdinalIgnoreCase) || keyToken.Equals("Back", StringComparison.OrdinalIgnoreCase))
        {
            key = Key.Back;
            return true;
        }
        if (keyToken.Equals("Delete", StringComparison.OrdinalIgnoreCase) || keyToken.Equals("Del", StringComparison.OrdinalIgnoreCase))
        {
            key = Key.Delete;
            return true;
        }
        if (keyToken.Equals("Space", StringComparison.OrdinalIgnoreCase) || keyToken.Equals("Spacebar", StringComparison.OrdinalIgnoreCase))
        {
            key = Key.Space;
            return true;
        }
        if (keyToken.Equals("Tab", StringComparison.OrdinalIgnoreCase))
        {
            key = Key.Tab;
            return true;
        }

        return Enum.TryParse(keyToken, ignoreCase: true, out key);
    }
}
