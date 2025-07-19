namespace Henchman.TaskManager;

internal sealed class TaskDescriptionScope : IDisposable
{
    private readonly string description;

    public TaskDescriptionScope(string description)
    {
        this.description = description;
        TaskDescription.Add(description);
    }

    public void Dispose()
    {
        TaskDescription.Remove(description);
    }
}
