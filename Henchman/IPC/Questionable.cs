using ECommons.EzIpcManager;

namespace Henchman.IPC;

[IPC(IPCNames.Questionable)]
public static class Questionable
{
    [EzIPC]
    public static Func<bool> IsRunning;

    [EzIPC]
    public static Func<string, bool> StartQuest;

    [EzIPC]
    public static Func<string, bool> StartSingleQuest;
}
