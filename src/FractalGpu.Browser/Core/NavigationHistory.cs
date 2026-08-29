using System.Collections.Generic;

namespace FractalGpu.Browser.Core;

/// <summary>One entry in the browsing history: where we were looking and at which sequence.</summary>
public readonly record struct Scene(FractalView View, string Pattern);

/// <summary>
/// Browser-style back/forward stack over <see cref="Scene"/>.
/// Zooming is destructive without one — the legacy app had no way back from a zoom at all.
/// </summary>
public sealed class NavigationHistory
{
    private const int Capacity = 200;

    private readonly List<Scene> _entries = [];
    private int _cursor = -1;

    public bool CanGoBack => _cursor > 0;
    public bool CanGoForward => _cursor >= 0 && _cursor < _entries.Count - 1;
    public Scene Current => _entries[_cursor];
    public bool IsEmpty => _cursor < 0;

    /// <summary>Records a new position, discarding any forward entries — exactly like a web browser.</summary>
    public void Push(Scene scene)
    {
        if (_cursor >= 0 && _entries[_cursor] == scene) return;

        if (_cursor < _entries.Count - 1)
            _entries.RemoveRange(_cursor + 1, _entries.Count - _cursor - 1);

        _entries.Add(scene);

        if (_entries.Count > Capacity)
            _entries.RemoveAt(0);

        _cursor = _entries.Count - 1;
    }

    /// <summary>
    /// Replaces the current entry instead of adding one. Used while a gesture is still in flight so
    /// a single drag does not leave a hundred history entries behind it.
    /// </summary>
    public void Replace(Scene scene)
    {
        if (_cursor < 0) Push(scene);
        else _entries[_cursor] = scene;
    }

    public bool TryGoBack(out Scene scene)
    {
        if (!CanGoBack) { scene = default; return false; }
        scene = _entries[--_cursor];
        return true;
    }

    public bool TryGoForward(out Scene scene)
    {
        if (!CanGoForward) { scene = default; return false; }
        scene = _entries[++_cursor];
        return true;
    }
}
