using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FryWorld.Sdk.Descriptors;

namespace FryWorld.Sdk.Shortcuts;

/// <summary>
/// Thread-safe, lock-free snapshotting implementation of <see cref="IShortcutRegistry"/>.
/// </summary>
public sealed class ShortcutRegistry : IShortcutRegistry
{
    private readonly Lock _lock = new();
    private readonly List<ShortcutBinding> _bindings = [];
    private readonly Dictionary<string, ShortcutBinding> _userOverrides = new(StringComparer.OrdinalIgnoreCase);
    private ShortcutBinding[] _snapshot = [];

    /// <inheritdoc />
    public event Action? Changed;

    /// <summary>
    /// Initializes a new instance of <see cref="ShortcutRegistry"/>.
    /// </summary>
    public ShortcutRegistry()
    {
        RebuildSnapshot();
    }

    /// <inheritdoc />
    public IDisposable Register(ShortcutBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        lock (_lock)
        {
            _bindings.Add(binding);
            RebuildSnapshot();
        }

        Changed?.Invoke();

        return new UnregisterHandle(this, binding);
    }

    /// <inheritdoc />
    public IDisposable Register(ShortcutDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var binding = new ShortcutBinding
        {
            Id = descriptor.Id,
            KeyGesture = descriptor.KeyGesture,
            Description = descriptor.Description,
            Title = descriptor.Title ?? descriptor.Description,
            Category = descriptor.Category,
            Scope = descriptor.Scope ?? ShortcutScope.Global,
            When = descriptor.When,
            WhenPredicate = descriptor.WhenPredicate,
            Priority = descriptor.Priority,
            Command = descriptor.Command,
            AsyncAction = descriptor.AsyncAction != null ? _ => descriptor.AsyncAction() : null,
            Tier = ShortcutTier.PluginDefault,
            IsEnabled = descriptor.IsEnabled
        };

        return Register(binding);
    }

    /// <inheritdoc />
    public IReadOnlyList<ShortcutBinding> GetRegisteredBindings() => _snapshot;

    /// <inheritdoc />
    public IReadOnlyList<ShortcutBinding> GetActiveBindings(IShortcutContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var snapshot = _snapshot;
        var activeScopes = context.ActiveScopes;

        var result = new List<ShortcutBinding>();

        foreach (var binding in snapshot)
        {
            if (!binding.IsEnabled)
                continue;

            // Scope match check: either global or present in active scopes
            bool scopeMatches = ShortcutScope.Matches(binding.Scope, activeScopes);

            if (!scopeMatches)
                continue;

            // When string condition check
            if (!string.IsNullOrWhiteSpace(binding.When) && !context.HasContextKey(binding.When))
                continue;

            // WhenPredicate condition check
            if (binding.WhenPredicate != null && !binding.WhenPredicate(context))
                continue;

            result.Add(binding);
        }

        // Order by Scope specificity (index in activeScopes, earlier is more specific), then Tier descending, then Priority descending
        result.Sort((a, b) =>
        {
            int scopeIndexA = IndexOfScope(activeScopes, a.Scope);
            int scopeIndexB = IndexOfScope(activeScopes, b.Scope);

            int scopeCompare = scopeIndexA.CompareTo(scopeIndexB);
            if (scopeCompare != 0) return scopeCompare;

            int tierCompare = ((int)b.Tier).CompareTo((int)a.Tier);
            if (tierCompare != 0) return tierCompare;

            return b.Priority.CompareTo(a.Priority);
        });

        return result;
    }

    private static int IndexOfScope(IReadOnlyList<string> scopes, string scope)
    {
        string normalized = ShortcutScope.Normalize(scope);
        for (int i = 0; i < scopes.Count; i++)
        {
            if (scopes[i].Equals(scope, StringComparison.OrdinalIgnoreCase) ||
                ShortcutScope.Normalize(scopes[i]).Equals(normalized, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return int.MaxValue;
    }

    /// <inheritdoc />
    public string? ResolveDisplayGesture(string commandId, string? scope = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        var snapshot = _snapshot;

        ShortcutBinding? best = null;

        foreach (var b in snapshot)
        {
            if (!b.IsEnabled) continue;

            bool matchesId = commandId.Equals(b.CommandId, StringComparison.OrdinalIgnoreCase) ||
                             commandId.Equals(b.Id, StringComparison.OrdinalIgnoreCase);

            if (!matchesId) continue;

            if (scope != null && !b.Scope.Equals(scope, StringComparison.OrdinalIgnoreCase) && !b.Scope.Equals(ShortcutScope.Global, StringComparison.OrdinalIgnoreCase))
                continue;

            if (best == null || (int)b.Tier > (int)best.Tier || ((int)b.Tier == (int)best.Tier && b.Priority > best.Priority))
            {
                best = b;
            }
        }

        return best?.ParsedGesture.ToDisplayString();
    }

    /// <inheritdoc />
    public void RegisterUserOverride(ShortcutBinding userOverride)
    {
        ArgumentNullException.ThrowIfNull(userOverride);

        var tier3 = new ShortcutBinding
        {
            Id = userOverride.Id,
            KeyGesture = userOverride.KeyGesture,
            CommandId = userOverride.CommandId,
            Command = userOverride.Command,
            AsyncAction = userOverride.AsyncAction,
            Scope = userOverride.Scope,
            When = userOverride.When,
            WhenPredicate = userOverride.WhenPredicate,
            Priority = userOverride.Priority,
            Tier = ShortcutTier.UserOverride,
            Title = userOverride.Title,
            Category = userOverride.Category,
            Description = userOverride.Description,
            IsEnabled = userOverride.IsEnabled
        };

        lock (_lock)
        {
            _userOverrides[tier3.Id] = tier3;
            RebuildSnapshot();
        }

        Changed?.Invoke();
    }

    /// <inheritdoc />
    public void ResetUserOverrides()
    {
        lock (_lock)
        {
            _userOverrides.Clear();
            RebuildSnapshot();
        }

        Changed?.Invoke();
    }

    private void Unregister(ShortcutBinding binding)
    {
        lock (_lock)
        {
            _bindings.Remove(binding);
            RebuildSnapshot();
        }

        Changed?.Invoke();
    }

    private void RebuildSnapshot()
    {
        // Combined list of base bindings plus user overrides (user overrides take precedence by Id or can be standalone)
        var list = new List<ShortcutBinding>(_bindings.Count + _userOverrides.Count);

        foreach (var b in _bindings)
        {
            if (_userOverrides.TryGetValue(b.Id, out var userOver))
            {
                list.Add(userOver);
            }
            else
            {
                list.Add(b);
            }
        }

        foreach (var kvp in _userOverrides)
        {
            if (!_bindings.Any(b => b.Id.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(kvp.Value);
            }
        }

        _snapshot = [.. list];
    }

    private sealed class UnregisterHandle : IDisposable
    {
        private ShortcutRegistry? _registry;
        private readonly ShortcutBinding _binding;

        public UnregisterHandle(ShortcutRegistry registry, ShortcutBinding binding)
        {
            _registry = registry;
            _binding = binding;
        }

        public void Dispose()
        {
            var reg = Interlocked.Exchange(ref _registry, null);
            reg?.Unregister(_binding);
        }
    }
}
