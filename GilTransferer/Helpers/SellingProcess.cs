using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Utility;
using ECommons;
using ECommons.DalamudServices.Legacy;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using GilTransferer.Enums;
using GilTransferer.Models;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.Helpers.ObjectExtensions;
using NoireLib.TaskQueue;
using System;
using System.Collections.Generic;
using System.Numerics;
using Callback = ECommons.Automation.Callback;

namespace GilTransferer.Helpers;

public static class SellingProcess
{
    public static void SetupAllMannequins(Scenario? scenario)
    {
        if (scenario == null || Service.LifestreamIPC.IsBusy())
            return;

        foreach (var mannequin in scenario.Mannequins)
        {
            NoireLogger.LogDebug($"Processing mannequin for BaseId {mannequin.BaseId}");
            ProcessMannequin(mannequin, scenario);
        }

        Service.TaskQueue.StartQueue();
    }

    public static unsafe void ProcessMannequin(Mannequin? mannequin, Scenario selectedScenario, bool startQueue = false)
    {
        if (mannequin == null)
            return;

        var baseId = mannequin.BaseId;
        var companionOwnerId = mannequin.CompanionOwnerId;

        var foundNpc = CommonHelper.FindMannequinNpc(mannequin);

        if (foundNpc == null)
        {
            NoireLogger.LogDebug($"Could not find mannequin NPC for BaseId {baseId}");
            return;
        }

        NoireLogger.LogDebug($"Found mannequin NPC for BaseId {baseId}: Name={foundNpc.Name}");

        TaskBuilder.Create("Enabling TextAdvance")
            .WithAction(task =>
            {
                if (!Service.TextAdvanceIPC.EnableExternalControl(new()))
                {
                    NoireLogger.PrintToChat(XivChatType.Echo, "Could not enable TextAdvance.", ColorHelper.HexToVector3("#FF1111"));
                    throw new InvalidOperationException("Could not enable TextAdvance.");
                }
            })
            .OnFailedOrCancelled((task, ex) =>
            {
                Service.StopQueue();
            })
            .EnqueueTo(Service.TaskQueue);

        if (NoireService.ObjectTable.LocalPlayer == null)
        {
            NoireLogger.LogDebug("Local player is null, cannot proceed with mannequin setup.");
            return;
        }

        MoveToUntilInReach(foundNpc);
        InteractWithTarget();
        OpenMannequinMerchantSetting();

        foreach (var (slotType, mannequinSlot) in mannequin.Slots)
        {
            if (mannequinSlot.AssignedCharacter == null)
                continue;
            EnsureSlotEmpty(slotType, mannequinSlot, selectedScenario);
            SetSlotOnMannequin(slotType, mannequinSlot, selectedScenario);
        }

        CloseMannequinMerchantSetting();

        TaskBuilder.AddAction(() => Service.TextAdvanceIPC.DisableExternalControl(), Service.TaskQueue, "Disable TextAdvance");

        if (startQueue)
            Service.TaskQueue.StartQueue();
    }

    public static void MoveToUntilInReach(ICharacter character)
    {
        TaskBuilder.Create("Waiting to be available")
            .WithCondition(task => !CommonHelper.IsOccupied())
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.Create("Target and move to Mannequin")
            .WithAction(task =>
            {
                NoireService.TargetManager.SetTarget(character);

                if (NoireService.ObjectTable.LocalPlayer == null)
                    return;

                if (Vector3.Distance(character.Position, NoireService.ObjectTable.LocalPlayer.Position) > 3.5f)
                    Service.LifestreamIPC.Move([character.Position]);
            })
            .WithCondition(task =>
            {
                if (NoireService.ObjectTable.LocalPlayer == null)
                    return false;

                if (Vector3.Distance(NoireService.ObjectTable.LocalPlayer.Position, character.Position) < 3.5f)
                {
                    Service.LifestreamIPC.Move([]);
                    return true;
                }

                return false;
            })
            .EnqueueTo(Service.TaskQueue);
    }

    public static unsafe void InteractWithTarget()
    {
        TaskBuilder.Create("Interact with Target")
            .WithCondition(task =>
            {
                var target = NoireService.TargetManager.Target;

                if (target == null)
                    return false;

                if (CommonHelper.IsOccupied())
                    return false;

                TargetSystem.Instance()->InteractWithObject(target.Struct(), false);
                return true;
            })
            .WithRetries(3, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5))
            .EnqueueTo(Service.TaskQueue);
    }

    public static unsafe void OpenMannequinMerchantSetting()
    {
        TaskBuilder.Create("Open Mannequin Merchant Setting")
            .WithCondition(task =>
            {
                if (!AddonHelper.TryGetReadyAddon("SelectString", out var addon))
                    return false;

                Callback.Fire(addon, true, 0); // Click "Select gear to sell."
                return true;
            })
            .EnqueueTo(Service.TaskQueue);
    }

    private static unsafe void EnsureSlotEmpty(SlotType slotType, MannequinSlot mannequinSlot, Scenario selectedScenario)
    {
        var finalPriceOfSlot = CommonHelper.GetFinalPriceOfSlot(slotType, mannequinSlot, selectedScenario);
        if (finalPriceOfSlot == null)
            return;

        var slotNumber = (uint)slotType;

        // Change below logic to check if slot has an item instead of using context menu
        // if not then just skip removal
        // if yes, check if sold.
        // If yes, callback 13 menu will remove the item automatically without additional steps.
        // if not sold then callback 13 + remove slot item by clicking "return to inventory" and clicking "yes" on confirmation

        // Should wait merchant setting actually interactable

        TaskBuilder.Create("Wait for MerchantSetting Ready")
            .WithCondition(task =>
            {
                if (!AddonHelper.TryGetReadyAddon("MerchantSetting", out var addon))
                    return false;

                Callback.Fire(addon, true, 13, slotNumber); // Right click on slot (if empty, will do nothing, if not empty will open context menu to remove item)
                return true;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.Create($"Try to open context menu for slot {slotNumber} for {mannequinSlot.AssignedCharacter!.UniqueId}")
            .WithCondition(task =>
            {
                if (AddonHelper.TryGetReadyAddon("ContextMenu", out var contextMenuAddon))
                {
                    // Context menu is open, meaning the slot has an item
                    task.Metadata = true;
                    return true;
                }
                task.Metadata = null;
                return false;
            })
            .WithTimeout(500.Milliseconds())
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.Create($"Remove item from slot {slotNumber}")
            .WithCondition(task =>
            {
                var previousTaskMetadata = TaskBuilder.GetMetadataFromTask<object?>(Service.TaskQueue, $"Try to open context menu for slot {slotNumber} for {mannequinSlot.AssignedCharacter!.UniqueId}");

                if (previousTaskMetadata == null)
                    return true;

                if (!AddonHelper.TryGetReadyAddon("ContextMenu", out var addon))
                    return false;

                Callback.Fire(addon, true, 0, 0, 0); // Click on "Return to Inventory" in context menu
                return true;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.Create($"Select yes")
            .WithCondition(task =>
            {
                var previousTaskMetadata = TaskBuilder.GetMetadataFromTask<object?>(Service.TaskQueue, $"Try to open context menu for slot {slotNumber} for {mannequinSlot.AssignedCharacter!.UniqueId}");

                if (previousTaskMetadata == null)
                    return true;

                if (!AddonHelper.TryGetReadyAddon("SelectYesno", out var addon))
                    return false;

                NoireLogger.LogDebug($"Confirming Fire callback SelectYesno");

                Callback.Fire(addon, true, 0); // Click on "Yes" in SelectYesno
                return true;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.Create($"Confirm removal for slot {slotNumber}")
            .WithCondition(task =>
            {
                task.Metadata = TaskBuilder.GetMetadataFromTask<object?>(Service.TaskQueue, $"Try to open context menu for slot {slotNumber} for {mannequinSlot.AssignedCharacter!.UniqueId}");

                if (task.Metadata == null)
                    return true;

                if (AddonHelper.TryGetReadyAddon("SelectYesno", out var addon))
                    return false;

                return true;
            })
            .WithDelay(task => task.Metadata == null ? 0.Milliseconds() : 500.Milliseconds())
            .EnqueueTo(Service.TaskQueue);
    }

    private static unsafe void SetSlotOnMannequin(SlotType slotType, MannequinSlot mannequinSlot, Scenario selectedScenario)
    {
        if (!Configuration.Instance.ItemsPerSlot.ContainsKey(slotType))
        {
            var message = $"Skipping slot {slotType} because there is no configured item to sell. Please add an item for this slot in the configuration.";
            NoireLogger.PrintToChat(message);
            NoireLogger.LogDebug(message);
            return;
        }

        var finalPriceOfSlot = CommonHelper.GetFinalPriceOfSlot(slotType, mannequinSlot, selectedScenario);
        if (finalPriceOfSlot == null)
        {
            NoireLogger.LogDebug($"Skipping slot {slotType}, target char has not enough gils.");
            return;
        }

        var slotNumber = (uint)slotType;

        // Should wait merchant setting actually interactable

        TaskBuilder.Create("Open Select Item Menu")
            .WithCondition(task =>
            {
                if (!AddonHelper.TryGetReadyAddon("MerchantSetting", out var addon))
                    return false;

                Callback.Fire(addon, true, 12, slotNumber);
                return true;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.Create($"Find Right Item For {mannequinSlot.AssignedCharacter?.UniqueId ?? "Unknown"}")
            .WithCondition(task =>
            {
                if (!AddonHelper.TryGetReadyAddon("MerchantEquipSelect", out var addon))
                    return false;

                List<int> nodes = [4];

                for (int i = 41001; i < 41051; i++) // Iterate 50 ?
                    nodes.Add(i);

                var itemSheet = ExcelSheetHelper.GetSheet<Item>();

                if (itemSheet == null)
                    return false;

                foreach (var nodeIndex in nodes)
                {
                    var node = GenericHelpers.GetNodeByIDChain(addon->RootNode, 1, 8, 13, nodeIndex, 3);
                    if (node != null)
                    {
                        var text = node->GetAsAtkTextNode()->NodeText.GetText();
                        if (!text.IsNullOrWhitespace())
                        {
                            NoireLogger.LogDebug($"Found item with text: {text}", "[SELLING DEBUG] ");

                            if (itemSheet.TryGetRow(Configuration.Instance.ItemsPerSlot[slotType], out var item))
                            {
                                NoireLogger.LogDebug($"Should be item Id: {item.RowId}, name: {item.Name}, Is same ? {item.Name == text}", "[SELLING DEBUG] ");
                                if (item.Name == text)
                                {
                                    var callback = nodeIndex == 4 ? 0 : nodeIndex - 41000;
                                    NoireLogger.LogDebug($"Selecting item with callback value of {callback} ({text})", "[SELLING DEBUG] ");
                                    task.Metadata = callback;
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.Create("Select Item In List To Equip Slot")
            .WithCondition(task =>
            {
                if (!AddonHelper.TryGetReadyAddon("MerchantEquipSelect", out var addon))
                    return false;

                var previousMetadata = TaskBuilder.GetMetadataFromTask<object?>(Service.TaskQueue, $"Find Right Item For {mannequinSlot.AssignedCharacter?.UniqueId ?? "Unknown"}");
                if (previousMetadata == null || previousMetadata is not int nodeIndex)
                    return false;

                NoireLogger.LogDebug($"Firing callback to select item with nodeIndex {nodeIndex}", "[SELLING DEBUG] ");
                Callback.Fire(addon, true, 19, nodeIndex);
                return true;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.Create("Get RetainerSell Addon")
            .WithCondition(task =>
            {
                if (AddonHelper.TryGetReadyAddon("RetainerSell", out var addon))
                {
                    Callback.Fire(addon, true, 2, (int)finalPriceOfSlot);
                    return true;
                }
                return false;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.Create("Confirm Selling Item")
            .WithCondition(task =>
            {
                if (AddonHelper.TryGetReadyAddon("RetainerSell", out var addon))
                {
                    Callback.Fire(addon, true, 0);
                    return true;
                }
                return false;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);
    }

    private static unsafe void CloseMannequinMerchantSetting()
    {
        TaskBuilder.Create("Close Mannequin Merchant Setting")
            .WithCondition(task =>
            {
                if (!AddonHelper.TryGetReadyAddon("MerchantSetting", out var addon))
                    return false;

                Callback.Fire(addon, true, 11, 0);
                return true;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.Create("Close Mannequin Merchant Setting")
            .WithCondition(task =>
            {
                if (!AddonHelper.TryGetReadyAddon("SelectString", out var addon))
                    return false;

                Callback.Fire(addon, true, 6);
                return true;
            })
            .EnqueueTo(Service.TaskQueue);
    }
}
