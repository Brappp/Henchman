using ECommons.EzIpcManager;

namespace Henchman.IPC;

//[IPC(IPCNames.AutoRetainer)]
public static class AutoRetainer
{
    [EzIPC]
    public static Action<bool, object> SetMultiModeEnabled;

    [EzIPC]
    public static Func<List<ulong>> GetRegisteredCIDs;

    //[EzIPC]
    //public static Func<ulong, OfflineCharacterData> GetOfflineCharacterData;

    [EzIPC]
    public static Action<string, object> RequestCharacterPostprocess;

    [EzIPC]
    public static Action<object> FinishCharacterPostprocessRequest;
}
