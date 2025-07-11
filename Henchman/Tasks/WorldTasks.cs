using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace Henchman.Tasks;

internal static class WorldTasks
{
    internal static async Task InteractWithByDataId(uint dataId)
    {
        await WaitUntilAsync(() => TargetNearestByDataId(dataId), $"Target {dataId}");
        await Task.Delay(GeneralDelayMs);
        unsafe
        {
            TargetSystem.Instance()->InteractWithObject(TargetSystem.Instance()->Target);
        }
    }

    internal static async Task<IGameObject> GetNearestGameObjectByDataId(uint dataId, CancellationToken token = default)
    {
        IGameObject? gameObject = null;
        while (gameObject == null)
        {
            token.ThrowIfCancellationRequested();
            if (!IsOccupied())
            {
                gameObject = Svc.Objects.Where(obj => obj.DataId == dataId && obj.IsTargetable)
                                .OrderBy(x => Player.DistanceTo(x))
                                .FirstOrDefault();

                if (gameObject != null) break;

                await Task.Delay(GeneralDelayMs, token);
            }
        }

        return gameObject;
    }

    internal static async Task<IGameObject?> GetNearestMobByNameId(uint nameId, bool moveOnTimeout = false, CancellationToken token = default)
    {
        IBattleNpc? gameObject = null;
        var         iterations = 0;
        while (gameObject == null)
        {
            token.ThrowIfCancellationRequested();
            if (!IsOccupied())
            {
                gameObject = Svc.Objects.OfType<IBattleNpc>()
                                .Where(bnpc => bnpc is { IsTargetable: true, IsDead: false } && bnpc.NameId == nameId && (bnpc.TargetObject == null || bnpc.TargetObject.Equals(Player.Object)))
                                .OrderBy(x => Player.DistanceTo(x))
                                .FirstOrDefault();

                if (gameObject != null) return gameObject;

                // 120 * 500 ms iterations to move on after 1 minute
                if (moveOnTimeout && iterations == 120) return null;

                await Task.Delay(GeneralDelayMs, token);
                iterations++;
            }
        }

        return gameObject;
    }

    internal static Task<bool> IsPlayerInObjectRange(uint dataId, float distance = 5f)
    {
        var x = Svc.Objects.Where(obj => obj.DataId == dataId && obj.IsTargetable)
                   .OrderBy(x => Player.DistanceTo(x))
                   .FirstOrDefault();
        return x == null
                       ? Task.FromResult(false)
                       : Task.FromResult(Player.DistanceTo(x) < distance);
    }

    internal static async Task<bool> TargetByEntityId(uint entityId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!IsOccupied())
        {
            var x = Svc.Objects.SearchByEntityId(entityId);
            if (x != null)
            {
                await Task.Delay(GeneralDelayMs, token);
                Svc.Targets.Target = x;
                return true;
            }
        }

        return false;
    }

    internal static async Task<bool> TargetByName(string targetName, CancellationToken token = default) => await TargetNearestByName([targetName], token);

    internal static async Task<bool> TargetNearestByName(IEnumerable<string> targetName, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!IsOccupied())
        {
            var gameObject = Svc.Objects.Where(obj => targetName.Contains(obj.Name.TextValue) && obj.IsTargetable)
                                .OrderBy(x => Player.DistanceTo(x))
                                .FirstOrDefault();
            if (gameObject != null)
            {
                await Task.Delay(GeneralDelayMs, token);
                Svc.Targets.Target = gameObject;
                return true;
            }
        }

        return false;
    }

    internal static async Task<bool> TargetNearestByDataId(uint dataId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!IsOccupied())
        {
            var x = Svc.Objects.Where(obj => obj.DataId == dataId && obj.IsTargetable)
                       .OrderBy(x => Player.DistanceTo(x))
                       .FirstOrDefault();
            if (x != null)
            {
                Svc.Targets.Target = x;
                await Task.Delay(GeneralDelayMs, token);
                return true;
            }
        }

        return false;
    }


    internal static async Task<bool> TargetNearestByDataId(IEnumerable<uint> dataId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!IsOccupied())
        {
            var x = Svc.Objects.Where(obj => dataId.Contains(obj.DataId) && obj.IsTargetable)
                       .OrderBy(x => Player.DistanceTo(x))
                       .FirstOrDefault();
            if (x != null)
            {
                await Task.Delay(100, token);
                Svc.Targets.Target = x;
                return true;
            }
        }

        return false;
    }
}
