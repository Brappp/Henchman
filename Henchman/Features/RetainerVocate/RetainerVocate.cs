using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using ECommons.Automation;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Henchman.Data;
using Henchman.Helpers;
using Henchman.Models;
using Henchman.TaskManager;
using Lumina.Excel.Sheets;

namespace Henchman.Features.RetainerVocate;

internal class RetainerVocate
{
    private static uint GilShopItemId(uint itemId) => Svc.Data.GetSubrowExcelSheet<GilShopItem>()
                                                         .Flatten()
                                                         .FirstOrDefault(x => x.Item.RowId ==
                                                                              itemId)
                                                         .RowId;

    private static GilShop GilShop(uint itemId) => Svc.Data.GetExcelSheet<GilShop>()
                                                      .GetRow(GilShopItemId(itemId));

    private static TopicSelect VendorShop(uint itemId) => Svc.Data.GetExcelSheet<TopicSelect>()
                                                             .FirstOrDefault(topicSelect => topicSelect.Shop.Any(shop => shop.RowId == GilShopItemId(itemId)));

    private static ClassJob ClassRow(uint retainerClass) => Svc.Data.GetExcelSheet<ClassJob>()
                                                               .GetRow(retainerClass);

    private static Item MainHand(uint retainerClass) => Svc.Data.GetExcelSheet<Item>()
                                                           .FirstOrDefault(item => item.ClassJobCategory.Value.Name.ExtractText()
                                                                                       .Contains(Svc.Data.GetExcelSheet<ClassJob>()
                                                                                                    .FirstOrDefault(classJob => classJob.RowId == retainerClass)
                                                                                                    .Abbreviation.ExtractText()) &&
                                                                                   item.Name.ExtractText()
                                                                                       .Contains("Weathered") &&
                                                                                   new[] { "Arm", "Grimoire", "Primary Tool" }.Any(category => item.ItemUICategory.Value
                                                                                                                                                   .Name.ExtractText()
                                                                                                                                                   .Contains(category)));

    internal static bool IsCombat(uint retainerClass) => ClassRow(retainerClass)
                                                       .ClassJobCategory.Value.Name.ExtractText()
                                                       .Contains("War") ||
                                                        ClassRow(retainerClass)
                                                               .ClassJobCategory.Value.Name.ExtractText()
                                                               .Contains("Magic");

    private static NpcData VendorData(uint retainerClass) => IsCombat(retainerClass)
                                                                     ? NpcDatabase.BeginnerDoWDoMVendor[C.RetainerCity]
                                                                     : NpcDatabase.BeginnerDoLVendor[C.RetainerCity];

    /*
     * Main Tasks
     */

    internal void RunFullCreation(uint retainerAmount, uint retainerClassId, uint combatClass)
    {
        EnqueueTask(new TaskRecord(GoToRetainerVocate, "Go To Retainer Vocate"));
        EnqueueTask(new TaskRecord(token => CreateRetainers(token, (int)retainerAmount), "Create Retainers"));
        if (!QuestManager.IsQuestComplete(66968) && !QuestManager.IsQuestComplete(66969) && !QuestManager.IsQuestComplete(66970))
        {
            if (!SubscriptionManager.IsInitialized(IPCNames.Questionable))
            {
                ChatPrint("'Questionable' not available. Skipping Venture Quest and equipping Retainers.");
                return;
            }

            EnqueueTask(new TaskRecord(token => StartVentureQuest(token, combatClass), "Do Retainer Venture Quest"));
        }

        EnqueueTask(new TaskRecord(token => BuyAndEquipRetainerGear(token, retainerAmount, retainerClassId), "Buy and Equip Retainer Gear"));
    }

    internal async Task GoToRetainerVocate(CancellationToken token = default)
    {
        if (token.IsCancellationRequested) return;
        var  retainerVocateData = NpcDatabase.RetainerVocates[C.RetainerCity];
        byte maxRetainerEntitlement;
        unsafe
        {
            maxRetainerEntitlement = RetainerManager.Instance()->MaxRetainerEntitlement;
        }

        if (Player.Territory != retainerVocateData.TerritoryId)

        {
            await TeleportTo(retainerVocateData.AetheryteTerritoryId, retainerVocateData.TerritoryId, retainerVocateData.AetheryteId, token);

            if (retainerVocateData.ZoneTransitionPosition != null)
                await MoveToNextZone(retainerVocateData.ZoneTransitionPosition.Value, retainerVocateData.TerritoryId, token);
        }

        if (Player.DistanceTo(retainerVocateData.InteractablePosition) > 5f) await MoveToStationaryObject(retainerVocateData.InteractablePosition, retainerVocateData.DataId, token: token);

        if (maxRetainerEntitlement == 0)
        {
            await CheckForRetainerEntitlement(retainerVocateData.DataId, token);

            await WaitUntilAsync(() => !Player.IsBusy, "Wait for not busy", token);
        }
    }

    internal async Task<bool> CreateRetainers(CancellationToken token = default, int createRetainerAmount = -1)
    {
        if (token.IsCancellationRequested) return false;
        int openRetainerAmount;
        unsafe
        {
            openRetainerAmount = RetainerManager.Instance()->MaxRetainerEntitlement - RetainerManager.Instance()->GetRetainerCount();
            ChatPrint($"Open: {openRetainerAmount.ToString()} | Create: {createRetainerAmount}");
        }

        if (openRetainerAmount == 0)
            return false;

        if (createRetainerAmount != -1)
        {
            if (createRetainerAmount > openRetainerAmount)
                createRetainerAmount = openRetainerAmount;
        }
        else
        {
            if (!C.UseMaxRetainerAmount)
            {
                createRetainerAmount = openRetainerAmount >= C.RetainerAmount + 1
                                               ? C.RetainerAmount + 1
                                               : openRetainerAmount;
            }
            else
                createRetainerAmount = openRetainerAmount;
        }


        for (var i = 0; i < createRetainerAmount; i++)
            await CreateSingleRetainer(token);

        return true;
    }
    
    internal async Task StartVentureQuest(CancellationToken token = default, uint combatClass = 0)
    {
        if (combatClass == 0)
            combatClass = C.QstClassJob;

        var classJob = Svc.Data.GetExcelSheet<ClassJob>()
                          .GetRow(combatClass);
        var gearset = Utils.GetGearsetForClassJob(classJob);
        unsafe
        {
            if (PlayerState.Instance()->CurrentClassJobId != classJob.RowId)
                ErrorIf(gearset == null, $"No gearset assigned for the chosen class {classJob.Name.ExtractText()}");
        }


        Svc.Commands.ProcessCommand($"/gearset change {gearset.Value + 1}");

        byte startTown;
        unsafe
        {
            startTown = PlayerState.Instance()->StartTown;
        }

        YesAlreadyManager.TemporarilyNeeded  = true;
        TextAdvanceManager.TemporarilyNeeded = true;

        switch (startTown)
        {
            case 1:
            {
                Questionable.StartSingleQuest("1433");
                await WaitUntilAsync(() => QuestManager.IsQuestComplete(66969), "Waiting for Quest 'An Ill-conceived Venture' (Limsa) to finish.", token);
                break;
            }
            case 2:
            {
                Questionable.StartSingleQuest("1432");
                await WaitUntilAsync(() => QuestManager.IsQuestComplete(66968), "Waiting for Quest 'An Ill-conceived Venture' (Gridania) to finish.", token);
                break;
            }
            case 3:
            {
                Questionable.StartSingleQuest("1434");
                await WaitUntilAsync(() => QuestManager.IsQuestComplete(66970), "Waiting for Quest 'An Ill-conceived Venture' (Ul'dah) to finish.", token);
                break;
            }
            default:
                Console.WriteLine("Unknown start town, no quest assigned.");
                break;
        }

        YesAlreadyManager.TemporarilyNeeded  = false;
        TextAdvanceManager.TemporarilyNeeded = false;
    }

    internal async Task BuyAndEquipRetainerGear(CancellationToken token = default, uint retainerAmount = 0, uint retainerClassId = 0)
    {
        byte maxRetainerEntitlement;
        bool anyRetainerNoJob;
        int  retainerAmountNoJob;
        unsafe
        {
            maxRetainerEntitlement = RetainerManager.Instance()->MaxRetainerEntitlement;
            anyRetainerNoJob = RetainerManager.Instance()->Retainers.ToArray()
                                                                    .Any(x => x is { ClassJob: 0, Available: true });
            retainerAmountNoJob = RetainerManager.Instance()->Retainers.ToArray()
                                                                       .Count(x => x is { ClassJob: 0, Available: true });
        }

        if (retainerAmount > retainerAmountNoJob)
            retainerAmount = (uint)retainerAmountNoJob;

        if (maxRetainerEntitlement > 0 && anyRetainerNoJob)
        {
            if (retainerClassId == 0)
                retainerClassId = C.RetainerClass;
            await GoToVendor(retainerClassId, token);
            await PurchaseStarterGear(retainerAmount, retainerClassId, token);
            await AssignRetainerClassEquipMain(retainerAmount, retainerClassId, token);
        }
    }


    private async Task GoToVendor(uint retainerClassId, CancellationToken token = default)
    {
        var vendorData = VendorData(retainerClassId);
        await TeleportTo(vendorData.AetheryteTerritoryId, vendorData.TerritoryId, vendorData.AetheryteId, token);

        if (vendorData.ZoneTransitionPosition != null)
            await MoveToNextZone(vendorData.ZoneTransitionPosition.Value, vendorData.TerritoryId, token);

        await MoveToStationaryObject(vendorData.InteractablePosition, vendorData.DataId, token: token);
    }

    private async Task PurchaseStarterGear(uint retainerAmount, uint retainerClassId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var vendorData = VendorData(retainerClassId);
        ChatPrint($"Targeting {vendorData.DataId}");
        await WaitUntilAsync(() => TargetNearestByDataId(vendorData.DataId, token), $"Target {vendorData.Name}", token);
        ChatPrint($"Opening Shop {vendorData.DataId} {retainerClassId}");
        Verbose(vendorData.DataId.ToString());
        Verbose(MainHand(retainerClassId)
               .RowId.ToString());
        Verbose(VendorShop(MainHand(retainerClassId)
                                  .RowId)
               .RowId.ToString());
        await WaitUntilAsync(() => ShopUtils.OpenShop(vendorData.DataId, VendorShop(MainHand(retainerClassId)
                                                                                           .RowId)
                                                             .RowId), "Open Shop", token);
        await WaitUntilAsync(() => TrySelectSpecificEntry(GilShop(MainHand(retainerClassId)
                                                                         .RowId)
                                                         .Name.ExtractText()), $"Try open \"{GilShop(MainHand(retainerClassId).RowId).Name.ExtractText()}\"", token);
        await WaitUntilAsync(() => ShopUtils.IsShopOpen(GilShop(MainHand(retainerClassId)
                                                                       .RowId)
                                                               .RowId), "Wait for Shop Open", token);

        byte maxRetainerEntitlement;
        unsafe
        {
            maxRetainerEntitlement = RetainerManager.Instance()->MaxRetainerEntitlement;
        }

        if (maxRetainerEntitlement > 0)
        {
            for (var i = 0;
                 i <=
                 retainerAmount -
                 1;
                 i++)
            {
                await WaitUntilAsync(() => ShopUtils.BuyItemFromShop(GilShop(MainHand(retainerClassId)
                                                                                    .RowId)
                                                                            .RowId, MainHand(retainerClassId)
                                                                            .RowId, 1), $"Buy Item {MainHand(retainerClassId).Name.ExtractText()}", token);
                await Task.Delay(GeneralDelayMs, token)
                          .ConfigureAwait(true);
            }
            /*if (C.UseMaxRetainerAmount)
            {
                for (var i = 0;
                     i <=
                     retainerAmount -
                     1;
                     i++)
                {
                    await WaitUntilAsync(() => ShopUtils.BuyItemFromShop(GilShop(retainerClassId)
                                                                                .RowId, MainHand(retainerClassId)
                                                                                .RowId, 1), $"Buy Item {MainHand(retainerClassId).Name.ExtractText()}", token);
                    await Task.Delay(GeneralDelayMs, token)
                              .ConfigureAwait(true);
                }
            }
            else
            {
                for (var i = 0; i <= C.RetainerAmount; i++)
                {
                    await WaitUntilAsync(() => ShopUtils.BuyItemFromShop(GilShop(retainerClassId)
                                                                                .RowId, MainHand(retainerClassId)
                                                                                .RowId, 1), $"Buy Item {MainHand(retainerClassId).Name.ExtractText()}", token);
                    await Task.Delay(GeneralDelayMs, token)
                              .ConfigureAwait(true);
                }
            }*/
        }

        await WaitUntilAsync(ShopUtils.CloseShop, "Close Shop", token);
        await WaitUntilAsync(() => TrySelectSpecificEntry(Lang.SelectStringCancel), "SelectString Cancel", token);
    }

    internal async Task AssignRetainerClassEquipMain(uint retainerAmount = 0, uint retainerClassId = 0, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        await MoveAndInteractWithClosestSummoningBell(token);

        uint retainerCount;
        unsafe
        {
            retainerCount = RetainerManager.Instance()->GetRetainerCount();
        }

        if (retainerAmount > retainerCount)
            retainerAmount = retainerCount;

        string classEntry;

        if (retainerClassId != 0)
        {
            ErrorIf(retainerClassId is not (>= 1 and <= 7 or >= 16 and <= 18 or 26 or 29), "No valid retainer class id passed!");
            classEntry = Svc.Data.GetExcelSheet<ClassJob>()
                            .GetRow(retainerClassId)
                            .Name.ExtractText();
        }
        else
        {
            classEntry = Svc.Data.GetExcelSheet<ClassJob>()
                            .GetRow(C.RetainerClass)
                            .Name.ExtractText();
        }
        PluginLog.Debug(retainerAmount.ToString());
        var index = -1;
        unsafe
        {
            for (var i = 0; i < retainerCount; i++)
            {
                if (RetainerManager.Instance()->Retainers[i].ClassJob == 0)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1) return;
        }

        for (var i = index; i < index + retainerAmount; i++)
        {
            var    pos = i;
            byte   classJob;
            string nameString;
            unsafe
            {
                classJob   = RetainerManager.Instance()->Retainers[pos].ClassJob;
                nameString = RetainerManager.Instance()->Retainers[pos].NameString;
            }
            if (classJob != 0) continue;
            await WaitUntilAsync(() => SelectRetainerInList(pos, token), $"Select Retainer {nameString}", token);
            await WaitUntilAsync(() => TrySelectSpecificEntry(Lang.SelectStringAssignRetainerClass), "SelectString AssignClass", token);
            await WaitUntilAsync(() => TrySelectSpecificEntry(classEntry), "SelectString Class", token);
            await WaitUntilAsync(() => RegexYesNo(true, Lang.SelectYesNoClassConfirmAsk), "SelectYesNo Confirm class", token);
            await WaitUntilAsync(() => TrySelectSpecificEntry(Lang.SelectStringNoMainEquipped), "SelectString Retainer Gear", token);
            await WaitUntilAsync(() => EquipRetainer(MainHand(retainerClassId), token),
                                 $"Equip {MainHand(retainerClassId).Name.ExtractText()} to Retainer {nameString}", token);
            await WaitUntilAsync(() => CloseRetainerCharacter(token), "CloseRetainerWindow", token);
            if (C.SendOnFirstExploration && InventoryHelper.GetInventoryItemCount(21072) > 2)
            {
                await WaitUntilAsync(() => TrySelectSpecificEntry(Lang.SelectStringAssignVenture), "SelectString Assign Venture", token);
                await WaitUntilAsync(() => TrySelectSpecificEntry([Lang.SelectStringVentureCategoryFieldExploration, Lang.SelectStringVentureCategoryHighlandExploration, Lang.SelectStringVentureCategoryWatersideExploration, Lang.SelectStringVentureCategoryWoodlandExploration]), "SelectString Retainer Gear", token);
                await WaitUntilAsync(() => TrySelectFirstExplorationVenture(classJob), "SelectString Exploration Task", token);
                await WaitUntilAsync(() => TryClickRetainerTaskAskAssign(), "Assign Venture", token);
            }
            await WaitUntilAsync(() => TrySelectSpecificEntry(Lang.SelectStringQuitWithDot), "SelectString Quit", token);
        }
        await WaitUntilAsync(() => CloseRetainerList(token), "Close RetainerList", token);
    }

    /*
     * Sub Tasks
     */

    private async Task MoveAndInteractWithClosestSummoningBell(CancellationToken token = default)
    {
        await WaitUntilAsync(() => PathfindAndMoveToBell(token), "Pathfind to Bell", token);
        UseSprint();
        await WaitUntilAsync(() => IsPlayerInObjectRange3D(GetNearestSummoningBell()!, 2f), "Check for distance", token);
        Vnavmesh.StopCompletely();
        await WaitUntilAsync(InteractWithSummoningBell, "Interact with Summoning Bell", token);
    }

    private async Task CheckForRetainerEntitlement(uint dataId, CancellationToken token = default)
    {
        await WaitUntilAsync(() => TargetNearestByDataId(dataId, token), "Target Retainer Vocate", token);
        await Task.Delay(GeneralDelayMs, token)
                  .ConfigureAwait(true);
        unsafe
        {
            TargetSystem.Instance()->InteractWithObject(TargetSystem.Instance()->Target, false);
        }

        byte maxRetainerEntitlement = 0;
        byte retainerCount          = 0;

        await WaitUntilAsync(async () =>
                             {
                                 unsafe
                                 {
                                     var manager = RetainerManager.Instance();
                                     maxRetainerEntitlement = manager->MaxRetainerEntitlement;
                                     retainerCount          = manager->GetRetainerCount();
                                 }

                                 return (maxRetainerEntitlement > 0 && maxRetainerEntitlement == retainerCount) || await TrySelectSpecificEntry(Lang.SelectStringHireARetainer);
                             }, "Wait for Retainer data or select 'Hire A Retainer' string", token);


        if (maxRetainerEntitlement != retainerCount)
            await WaitUntilAsync(() => ProcessYesNo(false, Lang.SelectYesNoHireARetainer), "SelectYesNo HireARetainer", token);
    }

    private async Task CreateSingleRetainer(CancellationToken token = default)
    {
        var retainerVocateData = NpcDatabase.RetainerVocates[C.RetainerCity];

        await WaitUntilAsync(() => TargetNearestByDataId(retainerVocateData.DataId, token), "Target Retainer Vocate", token);
        await Task.Delay(GeneralDelayMs, token)
                  .ConfigureAwait(true);
        unsafe
        {
            TargetSystem.Instance()->InteractWithObject(TargetSystem.Instance()->Target, false);
        }

        await WaitUntilAsync(() => TrySelectSpecificEntry(Lang.SelectStringHireARetainer), "SelectString HireARetainer", token);
        await WaitUntilAsync(() => ProcessYesNo(true, Lang.SelectYesNoHireARetainer), "SelectYesNo HireARetainer", token);
        await WaitUntilAsync(() => ProcessYesNo(false, Lang.SelectYesNoUseSavedAppearance), "SelectYesNo HireARetainer", token);
        await WaitUntilAsync(() => SelectRetainerRaceAndGender((int)C.RetainerRace + (int)C.RetainerGender, token), "Select Retainer Race and Gender", token);
        await WaitUntilAsync(() => RandomizeRetainerLook(token), "Randomize Retainer Look", token);
        await WaitUntilAsync(() => FinishRetainer(token), "Finish Retainer", token);
        await WaitUntilAsync(() => ProcessYesNo(false, Lang.SelectYesNoSaveAppearance), "SelectYesNo SaveAppearance", token);
        await WaitUntilAsync(() => ProcessYesNo(true, Lang.SelectYesNoFinalizeRetainer), "SelectYesNo FinalizeRetainer", token);
        await WaitUntilAsync(() => TrySelectSpecificEntry(Lang.SelectStringRetainerPersonality(C.RetainerPersonality)), "SelectString RetainerPersonality", token);
        await WaitUntilAsync(() => ProcessYesNo(true, Lang.SelectYesNoHireThisRetainer), "SelectYesNo HireThisRetainer", token);
        do
        {
            await WaitUntilAsync(() => InputRetainerName(token), "InputString RetainerName", token);
            await WaitUntilAsync(() => RegexYesNo(true, Lang.SelectStringHireTheServicesRetainer), "SelectYesNo HireTheServices", token);
            await Task.Delay(3000, token);
        }
        while (Svc.Condition[ConditionFlag.OccupiedInQuestEvent]);
    }

    private async Task<bool> SelectRetainerRaceAndGender(int raceGender, CancellationToken token = default)
    {
        unsafe
        {
            if (TryGetAddonByName<AtkUnitBase>("_CharaMakeRaceGender", out var charaMakeRaceGenderAddon) && IsAddonReady(charaMakeRaceGenderAddon))
            {
                if (TryGetAddonByName<AtkUnitBase>("_CharaMakeProgress", out var charaMakeProgessAddon) && IsAddonReady(charaMakeProgessAddon))
                {
                    Callback.Fire(charaMakeProgessAddon, true, 0, raceGender, 0, "", 0);
                    PluginLog.Debug($"Choosing Retainer Race {C.RetainerRace} and Gender {C.RetainerGender}");
                    return true;
                }
            }
        }

        await Task.Delay(GeneralDelayMs, token);
        return false;
    }

    private async Task<bool> RandomizeRetainerLook(CancellationToken token = default)
    {
        unsafe
        {
            if (TryGetAddonByName<AtkUnitBase>("_CharaMakeFeature", out var charaMakeFeatureAddon))
            {
                Callback.Fire(charaMakeFeatureAddon, true, -9, 0);
                PluginLog.Debug("Randomize Retainer Look");
                return true;
            }
        }

        await Task.Delay(GeneralDelayMs, token);
        return false;
    }

    private async Task<bool> FinishRetainer(CancellationToken token = default)
    {
        unsafe
        {
            if (TryGetAddonByName<AtkUnitBase>("_CharaMakeFeature", out var charaMakeFeatureAddon))
            {
                Callback.Fire(charaMakeFeatureAddon, true, 100);
                PluginLog.Debug("Finish Retainer");
                return true;
            }
        }

        await Task.Delay(GeneralDelayMs, token);
        return false;
    }

    private async Task<bool> InputRetainerName(CancellationToken token = default)
    {
        unsafe
        {
            if (TryGetAddonByName<AtkUnitBase>("InputString", out var inputString) && IsAddonReady(inputString))
            {
                Callback.Fire(inputString, true, 0, C.RetainerGender == RetainerDetails.RetainerGender.Male
                                                            ? NameGenerator.GetMasculineName
                                                            : NameGenerator.GetFeminineName, "");
                PluginLog.Debug("Input Retainer Name");
                return true;
            }
        }

        await Task.Delay(GeneralDelayMs, token);
        return false;
    }

    private async Task<bool> CloseRetainerCharacter(CancellationToken token = default)
    {
        unsafe
        {
            if (TryGetAddonByName<AtkUnitBase>("RetainerCharacter", out var retainerCharacterAddon))
            {
                Callback.Fire(retainerCharacterAddon, true, -1);
                PluginLog.Debug("Close Retainer Character window");
                return true;
            }
        }

        await Task.Delay(GeneralDelayMs, token);
        return false;
    }

    private async Task<bool> PathfindAndMoveToBell(CancellationToken token = default)
    {
        var bell = GetNearestSummoningBell();
        ErrorIf(bell == null, "Summoning Bell not found");
        if (bell != null)
        {
            Vnavmesh.SimpleMovePathfindAndMoveTo(bell.Position, false);
            return true;
        }

        await Task.Delay(GeneralDelayMs, token);
        return false;
    }

    private async Task<bool> SelectRetainerInList(int pos, CancellationToken token = default)
    {
        unsafe
        {
            if (TryGetAddonByName<AtkUnitBase>("RetainerList", out var retainerListAddon))
            {
                PluginLog.Debug(new AddonMaster.RetainerList(retainerListAddon).Retainers.Length.ToString());
                PluginLog.Debug(pos.ToString());
                new AddonMaster.RetainerList(retainerListAddon).Retainers[pos]
                                                               .Select();
                return true;
            }
        }

        await Task.Delay(GeneralDelayMs, token);
        return false;
    }

    private async Task<bool> CloseRetainerList(CancellationToken token = default)
    {
        unsafe
        {
            if (TryGetAddonByName<AtkUnitBase>("RetainerList", out var retainerListAddon))
            {
                Callback.Fire(retainerListAddon, true, -1);
                return true;
            }
        }

        await Task.Delay(GeneralDelayMs, token);
        return false;
    }

    private async Task<bool> EquipRetainer(Item mainHand, CancellationToken token = default)
    {
        unsafe
        {
            var itemCount = InventoryManager.Instance()->GetInventoryItemCount(mainHand.RowId, checkArmory: true);
            ErrorIf(itemCount == 0, "No gear for Retainer in inventory found.");

            if (InventoryManager.Instance()->GetInventoryContainer(InventoryType.RetainerEquippedItems)->GetInventorySlot(0)->ItemId == mainHand.RowId)
                return true;

            var inventoryTypes = new List<InventoryType>
                                 {
                                         InventoryType.ArmoryMainHand,
                                         InventoryType.Inventory1,
                                         InventoryType.Inventory2,
                                         InventoryType.Inventory3,
                                         InventoryType.Inventory4
                                 };

            foreach (var inventoryType in inventoryTypes)
            {
                if (InventoryManager.Instance()->GetItemCountInContainer(mainHand.RowId, inventoryType) > 0)
                {
                    var inv = InventoryManager.Instance()->GetInventoryContainer(inventoryType);
                    for (var i = 0; i < inv->Size; ++i)
                    {
                        if (inv->GetInventorySlot(i)->ItemId == mainHand.RowId)
                        {
                            InventoryManager.Instance()->MoveItemSlot(inventoryType, (ushort)inv->GetInventorySlot(i)->Slot, InventoryType.RetainerEquippedItems, 0,
                                                                      1);
                            return false;
                        }
                    }
                }
            }
        }

        await Task.Delay(GeneralDelayMs, token);
        return false;
    }

    private unsafe bool InteractWithSummoningBell()
    {
        TargetSystem.Instance()->InteractWithObject(GetNearestSummoningBell()
                                                           .Struct(), false);
        return TryGetAddonByName<AtkUnitBase>("RetainerList", out var retainerListAddon) && IsAddonReady(retainerListAddon);
    }

    private unsafe bool InteractWithVocate()
    {
        TargetSystem.Instance()->InteractWithObject(GetNearestSummoningBell()
                                                           .Struct(), false);
        return TryGetAddonByName<AtkUnitBase>("RetainerList", out var retainerListAddon) && IsAddonReady(retainerListAddon);
    }
}
