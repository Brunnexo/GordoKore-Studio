namespace GordoKore.Studio.Model;

/// <summary>
/// Desfazer/refazer por fotos do projeto (JSON). Cada edicao guarda o estado de antes; digitacao seguida (merge) vira um
/// passo so.
/// </summary>
public sealed class History(int limit = 200)
{
    private readonly List<string> _undo = new();
    private readonly Stack<string> _redo = new();
    private string _current = "";
    private bool _merging;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Reset(string state)
    {
        _undo.Clear();
        _redo.Clear();
        (_current, _merging) = (state, false);
    }

    /// <summary>Estado depois de uma edicao. Igual ao atual nao conta.</summary>
    public void Push(string state, bool merge = false)
    {
        if (state == _current)
            return;
        if (!(merge && _merging))
        {
            _undo.Add(_current);
            if (_undo.Count > limit)
                _undo.RemoveAt(0);
        }
        _redo.Clear();
        (_current, _merging) = (state, merge);
    }

    public string? Undo()
    {
        if (_undo.Count == 0)
            return null;
        _redo.Push(_current);
        (_current, _merging) = (_undo[^1], false);
        _undo.RemoveAt(_undo.Count - 1);
        return _current;
    }

    public string? Redo()
    {
        if (_redo.Count == 0)
            return null;
        _undo.Add(_current);
        (_current, _merging) = (_redo.Pop(), false);
        return _current;
    }
}
