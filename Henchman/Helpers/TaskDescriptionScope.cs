namespace Henchman.Helpers;

internal sealed class TaskDescriptionScope : IDisposable
{
    private readonly string _description;

    public TaskDescriptionScope(string description)
    {
        _description = description;
        TaskDescription.Add(_description);
    }

    public void Dispose()
    {
        TaskDescription.Remove(_description);
    }
}
