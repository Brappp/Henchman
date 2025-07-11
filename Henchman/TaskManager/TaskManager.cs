using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace Henchman.TaskManager;

public static class TaskManager
{
    private static readonly LinkedList<TaskRecord>              taskQueue = new();
    private static readonly LinkedList<CancellationTokenSource> ctsQuueue = new();
    public static           TaskRecord?                         CurrentTaskRecord;
    private static          CancellationTokenSource             Cts             = new();
    public static           List<string>                        TaskDescription = [];
    private static          Func<CancellationToken, Task>?      currentTask;
    private static          TaskRecord?                         ChainedTaskRecord;

    /// <summary>
    ///     General delay is 500 ms
    /// </summary>
    public static readonly int GeneralDelayMs = 500;

    public static bool Running => CurrentTaskRecord != null;

    public static string TaskName => Running
                                             ? CurrentTaskRecord!.Name
                                             : "No Task running";

    public static void EnqueueTask(TaskRecord task)
    {
        taskQueue.AddLast(task);
        ctsQuueue.AddLast(new CancellationTokenSource());
    }

    public static void EnqueueMultiTasks(TaskRecord[] tasks)
    {
        foreach (var task in tasks) taskQueue.AddLast(task);
    }

    public static void TaskManagerTick(IFramework framework)
    {
        if (ChainedTaskRecord != null && CurrentTaskRecord == null)
        {
            Cts               = new CancellationTokenSource();
            CurrentTaskRecord = ChainedTaskRecord;
            currentTask       = token => ChainedTaskRecord.Task(token);
            ChainedTaskRecord = CurrentTaskRecord.ChainedTaskRecord;

            Run();
        }
        else if (CurrentTaskRecord == null && taskQueue.TryGetFirst(out var nextTask) && ctsQuueue.TryGetFirst(out var cts))
        {
            Cts.Dispose();
            taskQueue.RemoveFirst();
            ctsQuueue.RemoveFirst();
            Cts               = cts;
            CurrentTaskRecord = nextTask;
            currentTask       = token => CurrentTaskRecord.Task(token);
            ChainedTaskRecord = CurrentTaskRecord.ChainedTaskRecord;

            Run();
        }
    }

    public static void Run()
    {
        Svc.Framework.Run(async () =>
                          {
                              try
                              {
                                  if (currentTask != null)
                                  {
                                      await currentTask.Invoke(Cts.Token)
                                                       .ConfigureAwait(true);
                                      ChatPrint($"{CurrentTaskRecord!.Name} completed!");
                                  }
                              }
                              catch (Exception ex)
                              {
                                  if (ex is not OperationCanceledException)
                                      PluginLog.Error($"Unexpected error in task execution: {ex}");

                                  CancelAllTasks();
                              } finally
                              {
                                  CurrentTaskRecord = null;
                                  currentTask       = null;
                                  TaskDescription.Clear();
                              }
                          }, Cts.Token)
           .ConfigureAwait(true);
    }

    public static void CancelAllTasks()
    {
        Cts.Cancel();
        taskQueue.Clear();
        ctsQuueue.Clear();
        CurrentTaskRecord = null;
        currentTask       = null;
        TaskDescription.Clear();
        AutoRotation.Disable();
    }
}
