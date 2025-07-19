using System.Linq;

namespace Henchman.TaskManager;

internal static class Logging
{
    internal static void Log(string     message) => PluginLog.Debug($"[{TaskName} - {(TaskDescription.Count   == 0 ? "No Description" : TaskDescription.Last())}]  {message}");

    internal static void Warning(string message)
    {
        ChatPrint($"[{TaskName} - {(TaskDescription.Count         == 0 ? "No Description" : TaskDescription.Last())}]  {message}");
        PluginLog.Warning($"[{TaskName} - {(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}]  {message}");
    }

    internal static bool WarningIf(bool condition, string message)
    {
        if (condition)
        {
            Warning(message);
            return true;
        }

        return false;
    }

    internal static void Error(string message)
    {
        //Log($"Error: [{TaskName} - {(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}]  {message}");
        Svc.Chat.PrintError($"[{TaskName} - {(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}] Plugin stopped! Error Reason: {message}");
        throw new Exception($"[{TaskName} - {(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}] {message}");
    }

    internal static void ErrorIf(bool condition, string message)
    {
        if (condition)
        {
            Error(message);
            CancelAllTasks();
        }
    }

    internal static void Verbose(string message) => PluginLog.Verbose($"[{TaskName} - {(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}]  {message}");

    internal static void Debug(string message) => PluginLog.Debug($"[{TaskName} - {(TaskDescription.Count == 0 ? "No Description" : TaskDescription.Last())}]  {message}");

    internal static void ChatPrint(string message) => Svc.Chat.Print($"[Henchman] {message}");
}
