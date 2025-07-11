using ECommons.EzIpcManager;

namespace Henchman.IPC;

[IPC(IPCNames.RotationSolverReborn)]
public static class RotationSolverReborn
{
    [EzIPC]
    public static Action<byte> ChangeOperatingMode;

    [EzIPC]
    public static Action<string> Test;

    // 0 = Off, 1 = Auto, 2 = Manual
    public static void Enable()
    {
        if (SubscriptionManager.IsInitialized(IPCNames.RotationSolverReborn))
            ChangeOperatingMode(2);
    }

    public static void Disable()
    {
        if (SubscriptionManager.IsInitialized(IPCNames.RotationSolverReborn))
            ChangeOperatingMode(0);
    }
}
