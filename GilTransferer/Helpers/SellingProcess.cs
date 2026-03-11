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
using System.Linq;
using System.Numerics;
using Callback = ECommons.Automation.Callback;

namespace GilTransferer.Helpers;

public static class SellingProcess
{
    public static void SetupAllMannequins(IEnumerable<Mannequin> mannequins, Scenario scenario)
    {
        if (mannequins == null || Service.LifestreamIPC.IsBusy())
            return;

        if (NoireService.ObjectTable.LocalPlayer == null)
        {
            NoireLogger.LogDebug("Local player is null, cannot proceed with mannequin setup.");
            return;
        }

        Service.TaskQueue.CreateTask("Enabling TextAdvance")
            .WithAction(task =>
            {
                if (!Service.TextAdvanceIPC.EnableExternalControl())
                {
                    NoireLogger.PrintToChat(XivChatType.Echo, "Could not enable TextAdvance.", ColorHelper.HexToVector3("#FF1111"));
                    throw new InvalidOperationException("Could not enable TextAdvance.");
                }
            })
            .OnFailedOrCancelled((task, ex) =>
            {
                Service.StopQueue();
            })
            .Enqueue();

        var mannequinsList = mannequins.ToList();

        foreach (var mannequin in mannequins)
            ProcessMannequin(mannequin, scenario);

        Service.TaskQueue.CreateTask("Disable TextAdvance")
            .WithAction(() => Service.TextAdvanceIPC.DisableExternalControl())
            .Enqueue();

        Service.TaskQueue.StartQueue();
    }

    private static void ProcessMannequin(Mannequin mannequin, Scenario selectedScenario)
    {
        if (mannequin == null)
            return;

        NoireLogger.LogDebug($"Processing mannequin for BaseId {mannequin.BaseId}");

        var baseId = mannequin.BaseId;
        var companionOwnerId = mannequin.CompanionOwnerId;

        var foundNpc = CommonHelper.FindMannequinNpc(mannequin);

        if (foundNpc == null)
        {
            NoireLogger.LogDebug($"Could not find mannequin NPC for BaseId {baseId}");
            return;
        }

        NoireLogger.LogDebug($"Found mannequin NPC for BaseId {baseId}: Name={foundNpc.Name}");

        Service.TaskQueue.CreateBatch($"Setting up mannequin {baseId}@{companionOwnerId}")
            .AddTasks(configurator =>
            {
                MoveToUntilInReach(configurator, foundNpc, mannequin);
                InteractWithTarget(configurator, foundNpc, mannequin);
                OpenMannequinMerchantSetting(configurator, mannequin);

                foreach (var (slotType, mannequinSlot) in mannequin.Slots)
                {
                    if (mannequinSlot.AssignedCharacter == null)
                        continue;
                    EnsureSlotEmpty(configurator, slotType, mannequinSlot, selectedScenario, mannequin);
                    SetSlotOnMannequin(configurator, slotType, mannequinSlot, selectedScenario, mannequin);
                }

                CloseMannequinMerchantSetting(configurator, mannequin);
            })
            .Enqueue();
    }

    private static void MoveToUntilInReach(BatchTaskConfigurator configurator, ICharacter character, Mannequin mannequin)
    {
        configurator.Create($"Waiting to be available for mannequin {mannequin.UniqueId}")
            .WithCondition(task => !CommonHelper.IsOccupied())
            .Enqueue();

        configurator.Create("Target and move to Mannequin")
            .WithAction(task =>
            {
                if (NoireService.ObjectTable.LocalPlayer == null)
                    return;

                if (Vector3.Distance(character.Position, NoireService.ObjectTable.LocalPlayer.Position) > 3.5f)
                    Service.LifestreamIPC.Move([character.Position]);
            })
            .WithCondition(() => IsWithinReach(character))
            .Enqueue();
    }

    private static bool IsWithinReach(ICharacter character)
    {
        if (NoireService.ObjectTable.LocalPlayer == null)
            return false;

        if (Vector3.Distance(NoireService.ObjectTable.LocalPlayer.Position, character.Position) < 3.5f)
        {
            Service.LifestreamIPC.Move([]);
            return true;
        }

        return false;
    }

    private static unsafe void InteractWithTarget(BatchTaskConfigurator configurator, ICharacter mannequinCharacter, Mannequin mannequin)
    {
        configurator.Create($"Interact with Target for mannequin {mannequin.UniqueId}")
            .WithAction(() => NoireService.TargetManager.SetTarget(mannequinCharacter))
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
            .WithRetries(3, 2.Seconds())
            .Enqueue();
    }

    private static unsafe void OpenMannequinMerchantSetting(BatchTaskConfigurator configurator, Mannequin mannequin)
    {
        configurator.Create($"Open Mannequin Merchant Setting for mannequin {mannequin.UniqueId}")
            .WithCondition(task =>
            {
                if (!AddonHelper.TryGetReadyAddon("SelectString", out var addon))
                    return false;

                Callback.Fire(addon, true, 0); // Click "Select gear to sell."
                return true;
            })
            .Enqueue();
    }

    private static unsafe void EnsureSlotEmpty(BatchTaskConfigurator configurator, SlotType slotType, MannequinSlot mannequinSlot, Scenario selectedScenario, Mannequin mannequin)
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

        configurator.Create($"Wait for MerchantSetting Ready for mannequin {mannequin.UniqueId}")
            .WithCondition(task =>
            {
                if (!AddonHelper.TryGetReadyAddon("MerchantSetting", out var addon))
                    return false;

                Callback.Fire(addon, true, 13, slotNumber); // Right click on slot (if empty, will do nothing, if not empty will open context menu to remove item)
                return true;
            })
            .WithDelay(500.Milliseconds())
            .Enqueue();

        configurator.Create($"Try to get context menu for slot {slotNumber} for mannequin {mannequin.UniqueId}")
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
            .Enqueue();

        configurator.Create($"Remove item from slot {slotNumber} for mannequin {mannequin.UniqueId}")
            .WithCondition(task =>
            {
                var previousTaskMetadata = TaskBuilder.GetMetadataFromTask<object?>(Service.TaskQueue, $"Try to get context menu for slot {slotNumber} for mannequin {mannequin.UniqueId}");

                if (previousTaskMetadata == null)
                    return true;

                if (!AddonHelper.TryGetReadyAddon("ContextMenu", out var addon))
                    return false;

                Callback.Fire(addon, true, 0, 0, 0); // Click on "Return to Inventory" / "Remove sold out item" in context menu
                return true;
            })
            .Enqueue();

        configurator.Create($"Select yes for slot {slotNumber} for mannequin {mannequin.UniqueId}")
            .WithCondition(task =>
            {
                var previousTaskMetadata = TaskBuilder.GetMetadataFromTask<object?>(Service.TaskQueue, $"Try to get context menu for slot {slotNumber} for mannequin {mannequin.UniqueId}");

                if (previousTaskMetadata == null)
                    return true;

                // If "Return to Inventory", SelectYesno will appear
                // If "Remove sold out item", no confirmation will appear, item will be removed immediately, we just timeout after 500 ms

                if (!AddonHelper.TryGetReadyAddon("SelectYesno", out var addon))
                    return false;

                NoireLogger.LogDebug($"Confirming Fire callback SelectYesno");

                Callback.Fire(addon, true, 0); // Click on "Yes" in SelectYesno
                return true;
            })
            .WithTimeout(500.Milliseconds())
            .Enqueue();

        configurator.Create($"Confirm removal for slot {slotNumber} for mannequin {mannequin.UniqueId}")
            .WithCondition(task =>
            {
                task.Metadata = TaskBuilder.GetMetadataFromTask<object?>(Service.TaskQueue, $"Try to get context menu for slot {slotNumber} for mannequin {mannequin.UniqueId}");

                if (task.Metadata == null)
                    return true;

                if (AddonHelper.TryGetReadyAddon("SelectYesno", out var addon))
                    return false;

                return true;
            })
            .WithDelay(task => task.Metadata == null ? 0.Milliseconds() : 500.Milliseconds())
            .Enqueue();
    }

    private static unsafe void SetSlotOnMannequin(BatchTaskConfigurator configurator, SlotType slotType, MannequinSlot mannequinSlot, Scenario selectedScenario, Mannequin mannequin)
    {
        if (!Configuration.Instance.ItemsPerSlot.ContainsKey(slotType))
        {
            var message = $"Skipping slot {slotType} for mannequin {mannequin.UniqueId} because there is no configured item to sell. Please add an item for this slot in the configuration.";
            NoireLogger.PrintToChat(message);
            NoireLogger.LogDebug(message);
            return;
        }

        var finalPriceOfSlot = CommonHelper.GetFinalPriceOfSlot(slotType, mannequinSlot, selectedScenario);
        if (finalPriceOfSlot == null)
        {
            NoireLogger.LogDebug($"Skipping slot {slotType} for mannequin {mannequin.UniqueId}, target char has not enough gils.");
            return;
        }

        var slotNumber = (uint)slotType;

        // Should wait merchant setting actually interactable

        configurator.Create($"Open Select Item Menu for slot {slotType} for mannequin {mannequin.UniqueId}")
            .WithCondition(task =>
            {
                if (!AddonHelper.TryGetReadyAddon("MerchantSetting", out var addon))
                    return false;

                Callback.Fire(addon, true, 12, slotNumber);
                return true;
            })
            .Enqueue();

        configurator.Create($"Find Right Item For for slot {slotType} for mannequin {mannequin.UniqueId}")
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
            .Enqueue();

        configurator.Create($"Select Item In List To Equip for slot {slotType} for mannequin {mannequin.UniqueId}")
            .WithCondition(task =>
            {
                if (!AddonHelper.TryGetReadyAddon("MerchantEquipSelect", out var addon))
                    return false;

                var previousMetadata = TaskBuilder.GetMetadataFromTask<object?>(Service.TaskQueue, $"Find Right Item For for slot {slotType} for mannequin {mannequin.UniqueId}");
                if (previousMetadata == null || previousMetadata is not int nodeIndex)
                    return false;

                NoireLogger.LogDebug($"Firing callback to select item with nodeIndex {nodeIndex}", "[SELLING DEBUG] ");
                Callback.Fire(addon, true, 19, nodeIndex);
                return true;
            })
            .Enqueue();

        configurator.Create($"Get RetainerSell Addon for slot {slotType} for mannequin {mannequin.UniqueId}")
            .WithCondition(task =>
            {
                if (AddonHelper.TryGetReadyAddon("RetainerSell", out var addon))
                {
                    Callback.Fire(addon, true, 2, (int)finalPriceOfSlot);
                    return true;
                }
                return false;
            })
            .Enqueue();

        configurator.Create($"Confirm Selling Item for slot {slotType} for mannequin {mannequin.UniqueId}")
            .WithCondition(task =>
            {
                if (AddonHelper.TryGetReadyAddon("RetainerSell", out var addon))
                {
                    Callback.Fire(addon, true, 0);
                    return true;
                }
                return false;
            })
            .Enqueue();

        configurator.Create()
            .WithDelay(500.Milliseconds())
            .Enqueue();
    }

    private static unsafe void CloseMannequinMerchantSetting(BatchTaskConfigurator configurator, Mannequin mannequin)
    {
        configurator.Create($"Close Mannequin Merchant Setting for mannequin {mannequin.UniqueId}")
            .WithCondition(task =>
            {
                if (!AddonHelper.TryGetReadyAddon("MerchantSetting", out var addon))
                    return false;

                Callback.Fire(addon, true, 11, 0);
                return true;
            })
            .Enqueue();

        configurator.Create($"Close Mannequin Merchant Setting for mannequin {mannequin.UniqueId}")
            .WithCondition(task =>
            {
                if (!AddonHelper.TryGetReadyAddon("SelectString", out var addon))
                    return false;

                Callback.Fire(addon, true, 6);
                return true;
            })
            .Enqueue();
    }
}
