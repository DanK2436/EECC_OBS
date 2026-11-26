namespace EeccStudio.Core.Models;

public sealed class StudioUndoStack
{
    private readonly Stack<Entry> _undo = new();
    private readonly Stack<Entry> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? NextUndo => _undo.Count > 0 ? _undo.Peek().Name : null;
    public string? NextRedo => _redo.Count > 0 ? _redo.Peek().Name : null;

    public void Push(string name, Action undo, Action redo)
    {
        _undo.Push(new Entry(name, undo, redo));
        _redo.Clear();
    }

    public bool TryUndo()
    {
        if (_undo.Count == 0)
            return false;
        var entry = _undo.Pop();
        entry.Undo();
        _redo.Push(entry);
        return true;
    }

    public bool TryRedo()
    {
        if (_redo.Count == 0)
            return false;
        var entry = _redo.Pop();
        entry.Redo();
        _undo.Push(entry);
        return true;
    }

    private readonly record struct Entry(string Name, Action Undo, Action Redo);
}
