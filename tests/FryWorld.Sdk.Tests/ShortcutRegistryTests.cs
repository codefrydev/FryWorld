using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Shortcuts;
using Xunit;

namespace FryWorld.Sdk.Tests;

public class ShortcutRegistryTests
{
    [Fact]
    public void PlatformKeyGesture_ParsesSingleKeysAndModifiers()
    {
        var g1 = PlatformKeyGesture.Parse("W");
        Assert.False(g1.IsChord);
        Assert.Equal(Key.W, g1.PrimaryStroke.Key);
        Assert.Equal(KeyModifiers.None, g1.PrimaryStroke.Modifiers);

        var g2 = PlatformKeyGesture.Parse("Mod+S");
        Assert.False(g2.IsChord);
        Assert.Equal(Key.S, g2.PrimaryStroke.Key);
        var expectedMod = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        Assert.Equal(expectedMod, g2.PrimaryStroke.Modifiers);

        var g3 = PlatformKeyGesture.Parse("Mod+Shift+P");
        Assert.Equal(Key.P, g3.PrimaryStroke.Key);
        Assert.True(g3.PrimaryStroke.Modifiers.HasFlag(KeyModifiers.Shift));
        Assert.True(g3.PrimaryStroke.Modifiers.HasFlag(expectedMod));
    }

    [Fact]
    public void PlatformKeyGesture_ParsesAndIdentifiesChords()
    {
        var chord = PlatformKeyGesture.Parse("Mod+K, Mod+C");
        Assert.True(chord.IsChord);
        Assert.Equal(2, chord.Strokes.Count);
        Assert.Equal(Key.K, chord.Strokes[0].Key);
        Assert.Equal(Key.C, chord.Strokes[1].Key);
    }

    [Theory]
    [InlineData("Mod+,", Key.OemComma)]
    [InlineData("Mod++", Key.OemPlus)]
    [InlineData(",", Key.OemComma)]
    [InlineData("+", Key.OemPlus)]
    [InlineData("Mod+Comma", Key.OemComma)]
    [InlineData("Mod+Plus", Key.OemPlus)]
    public void PlatformKeyGesture_ParsesPunctuationAndSymbolGestures(string gestureText, Key expectedKey)
    {
        var gesture = PlatformKeyGesture.Parse(gestureText);
        Assert.False(gesture.IsChord);
        Assert.Equal(expectedKey, gesture.PrimaryStroke.Key);
    }

    [Fact]
    public void PlatformKeyGesture_ParsesChordsWithPunctuationKeys()
    {
        var chord = PlatformKeyGesture.Parse("Mod+K, Mod+,");
        Assert.True(chord.IsChord);
        Assert.Equal(2, chord.Strokes.Count);
        Assert.Equal(Key.K, chord.Strokes[0].Key);
        Assert.Equal(Key.OemComma, chord.Strokes[1].Key);
    }

    [Fact]
    public void ShortcutRegistry_RegistersAndUnregistersCleanly()
    {
        var registry = new ShortcutRegistry();
        bool changedFired = false;
        registry.Changed += () => changedFired = true;

        var binding = new ShortcutBinding
        {
            Id = "circuit.wire",
            KeyGesture = "W",
            Scope = "plugin:circuit"
        };

        var handle = registry.Register(binding);
        Assert.True(changedFired);
        Assert.Contains(registry.GetRegisteredBindings(), b => b.Id == "circuit.wire");

        changedFired = false;
        handle.Dispose();
        Assert.True(changedFired);
        Assert.DoesNotContain(registry.GetRegisteredBindings(), b => b.Id == "circuit.wire");
    }

    [Fact]
    public void ShortcutRegistry_FiltersActiveBindingsByScope()
    {
        var registry = new ShortcutRegistry();

        registry.Register(new ShortcutBinding
        {
            Id = "circuit.wire",
            KeyGesture = "W",
            Scope = "plugin:circuit"
        });

        registry.Register(new ShortcutBinding
        {
            Id = "core.palette",
            KeyGesture = "Mod+K",
            Scope = ShortcutScope.Global
        });

        var circuitCtx = new ShortcutContext(["plugin:circuit"]);
        var activeInCircuit = registry.GetActiveBindings(circuitCtx);
        Assert.Contains(activeInCircuit, b => b.Id == "circuit.wire");
        Assert.Contains(activeInCircuit, b => b.Id == "core.palette");

        var csharpCtx = new ShortcutContext(["plugin:csharp"]);
        var activeInCSharp = registry.GetActiveBindings(csharpCtx);
        Assert.DoesNotContain(activeInCSharp, b => b.Id == "circuit.wire");
        Assert.Contains(activeInCSharp, b => b.Id == "core.palette");
    }

    [Fact]
    public void ShortcutRegistry_UserOverrideTakesPrecedence()
    {
        var registry = new ShortcutRegistry();

        registry.Register(new ShortcutBinding
        {
            Id = "circuit.wire",
            KeyGesture = "W",
            Scope = "plugin:circuit",
            Tier = ShortcutTier.PluginDefault
        });

        Assert.Equal("W", registry.ResolveDisplayGesture("circuit.wire", "plugin:circuit"));

        // User overrides "W" to "Alt+W"
        registry.RegisterUserOverride(new ShortcutBinding
        {
            Id = "circuit.wire",
            KeyGesture = "Alt+W",
            Scope = "plugin:circuit"
        });

        var active = registry.GetActiveBindings(new ShortcutContext(["plugin:circuit"]));
        var wireBinding = active.First(b => b.Id == "circuit.wire");
        Assert.Equal(ShortcutTier.UserOverride, wireBinding.Tier);
        Assert.Equal(Key.W, wireBinding.ParsedGesture.PrimaryStroke.Key);
        Assert.True(wireBinding.ParsedGesture.PrimaryStroke.Modifiers.HasFlag(KeyModifiers.Alt));

        // Reset restores default
        registry.ResetUserOverrides();
        active = registry.GetActiveBindings(new ShortcutContext(["plugin:circuit"]));
        wireBinding = active.First(b => b.Id == "circuit.wire");
        Assert.Equal(ShortcutTier.PluginDefault, wireBinding.Tier);
        Assert.Equal(KeyModifiers.None, wireBinding.ParsedGesture.PrimaryStroke.Modifiers);
    }

    private sealed class CustomSearchTextBox { }
    private sealed class CircuitCanvasControl { }

    [Fact]
    public void ShortcutManager_InputShielding_PreventsCharacterCaptureInTextBox()
    {
        var textBox = new CustomSearchTextBox();
        var canvas = new CircuitCanvasControl();

        // Plain 'W' key press (no modifiers)
        var plainArgs = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.W,
            KeyModifiers = KeyModifiers.None
        };

        // In TextBox: shielded (returns true -> shortcut processing bypassed)
        Assert.True(ShortcutManager.IsInputShielded(textBox, plainArgs));

        // In Canvas: not shielded (returns false -> shortcut processing allowed)
        Assert.False(ShortcutManager.IsInputShielded(canvas, plainArgs));

        // Modifier key (Cmd/Ctrl+S): never shielded even in TextBox!
        var modArgs = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.S,
            KeyModifiers = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control
        };
        Assert.False(ShortcutManager.IsInputShielded(textBox, modArgs));
    }

    [Fact]
    public void ShortcutManager_ExecutesMatchingCommandAndMarksHandled()
    {
        var registry = new ShortcutRegistry();
        var manager = new ShortcutManager(registry);
        manager.SetActiveScope("plugin:circuit");

        bool commandExecuted = false;
        registry.Register(new ShortcutBinding
        {
            Id = "circuit.tool.wire",
            KeyGesture = "W",
            Scope = "plugin:circuit",
            Command = new RelayCommand(() => commandExecuted = true)
        });

        var keyArgs = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.W,
            KeyModifiers = KeyModifiers.None
        };

        bool handled = manager.ProcessKeyDown(keyArgs, new ShortcutContext(["plugin:circuit"]));
        Assert.True(handled);
        Assert.True(keyArgs.Handled);
        Assert.True(commandExecuted);
    }

    [Fact]
    public void ScopedPluginContext_UnmountRollback_UnregistersAllShortcuts()
    {
        var rootCtx = new FryPluginContext();
        var scope = new PluginScope();
        var scopedCtx = rootCtx.CreateScopedContext(scope);

        scopedCtx.RegisterShortcut(new ShortcutDescriptor
        {
            Id = "test.shortcut",
            Description = "Test shortcut",
            KeyGesture = "Mod+T"
        });

        Assert.NotEmpty(rootCtx.GetRegisteredShortcuts());
        Assert.NotEmpty(rootCtx.Shortcuts.GetRegisteredBindings());

        // Dispose scope (simulates plugin unmount)
        scope.Dispose();

        Assert.Empty(rootCtx.GetRegisteredShortcuts());
        Assert.Empty(rootCtx.Shortcuts.GetRegisteredBindings());
    }

    [Fact]
    public void ShortcutManager_RegisterCommandHandler_DispatchesByCommandId()
    {
        var registry = new ShortcutRegistry();
        var manager = new ShortcutManager(registry);
        manager.SetActiveScope("com.fryworld.circuit");

        // Binding only specifies CommandId (no direct Command instance attached)
        registry.Register(new ShortcutBinding
        {
            Id = "circuit.wire.binding",
            CommandId = "circuit.tool.wire",
            KeyGesture = "W",
            Scope = "com.fryworld.circuit"
        });

        int executeCount = 0;
        var handle = manager.RegisterCommandHandler("circuit.tool.wire", () => executeCount++);

        var keyArgs = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.W,
            KeyModifiers = KeyModifiers.None
        };

        bool handled = manager.ProcessKeyDown(keyArgs, new ShortcutContext(["com.fryworld.circuit"]));
        Assert.True(handled);
        Assert.True(keyArgs.Handled);
        Assert.Equal(1, executeCount);

        // After disposing the handler, it shouldn't execute
        handle.Dispose();
        var keyArgs2 = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.W,
            KeyModifiers = KeyModifiers.None
        };
        bool handled2 = manager.ProcessKeyDown(keyArgs2, new ShortcutContext(["com.fryworld.circuit"]));
        Assert.False(handled2);
        Assert.False(keyArgs2.Handled);
        Assert.Equal(1, executeCount);
    }

    [Fact]
    public void ShortcutManager_RegisterCommandHandler_WithICommand_RespectsCanExecute()
    {
        var registry = new ShortcutRegistry();
        var manager = new ShortcutManager(registry);

        registry.Register(new ShortcutBinding
        {
            Id = "circuit.device.rotate",
            CommandId = "circuit.device.rotate",
            KeyGesture = "R",
            Scope = ShortcutScope.Global
        });

        bool canExecute = false;
        bool executed = false;
        var command = new RelayCommand(() => executed = true, () => canExecute);

        using var handle = manager.RegisterCommandHandler("circuit.device.rotate", command);

        var keyArgs = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.R,
            KeyModifiers = KeyModifiers.None
        };

        // When canExecute is false, does not handle
        bool handled = manager.ProcessKeyDown(keyArgs);
        Assert.False(handled);
        Assert.False(executed);

        // When canExecute is true, handles and executes
        canExecute = true;
        var keyArgs2 = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.R,
            KeyModifiers = KeyModifiers.None
        };
        bool handled2 = manager.ProcessKeyDown(keyArgs2);
        Assert.True(handled2);
        Assert.True(executed);
    }
}
