using System.Linq;
using System.Reflection;
using Dalamud.Plugin.Services;
using ECommons.EzIpcManager;

namespace Henchman.IPC;

internal static class SubscriptionManager
{
    private static readonly HashSet<string> InitializedIPCs = new();


    internal static bool IsInitialized(string plugin) => InitializedIPCs.Contains(plugin) && IsLoaded(plugin);

    internal static bool IsLoaded(string pluginName)
    {
        return Svc.PluginInterface.InstalledPlugins.Any(x => x.InternalName == pluginName && x.IsLoaded);
    }

    public static void Subscribe(IFramework framework)
    {
        try
        {
            EzIPC.Init(typeof(IPCProvider), "Henchman");
            foreach (var type in Assembly.GetExecutingAssembly()
                                         .GetTypes()
                                         .Where(type => type.GetCustomAttribute<IPCAttribute>() != null))
            {
                var attr = type.GetCustomAttribute<IPCAttribute>();
                if (!IsInitialized(attr.Name))
                {
                    if (IsLoaded(attr.Name))
                    {
                        // Why has RSR a different IPC prefix than its internal name!? (╯°□°)╯︵ ┻━┻
                        EzIPC.Init(type, attr.Name == "RotationSolver"
                                                 ? "RotationSolverReborn"
                                                 : attr.Name);
                        InitializedIPCs.Add(attr.Name);
                        PluginLog.Debug($"{attr.Name} IPC registered.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error("Could not subscribe to IPCs");
        }
    }
}
