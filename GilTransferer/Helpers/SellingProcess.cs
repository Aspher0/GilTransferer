using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Utility;
using ECommons;
using ECommons.DalamudServices.Legacy;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Component.GUI;
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
    public static void SetupAllMannequins(Scenario? scenario)
    {
        if (scenario == null || Service.LifestreamIPC.IsBusy())
            return;

        foreach (var mannequin in scenario.Mannequins)
        {
            NoireLogger.LogDebug($"Processing mannequin for BaseId {mannequin.BaseId} at position {mannequin.Position}");
            ProcessMannequin(mannequin);
        }

        Service.TaskQueue.StartQueue();
    }

    public static unsafe void ProcessMannequin(Mannequin? mannequin, bool startQueue = false)
    {
        if (mannequin == null)
            return;

        var baseId = mannequin.BaseId;
        var companionOwnerId = mannequin.CompanionOwnerId;
        var position = mannequin.Position;

        var allNpcs = NoireService.ObjectTable.OfType<INpc>();
        var foundNpc = allNpcs.FirstOrDefault((npc) =>
        {
            var native = CharacterHelper.GetCharacterAddress(npc);
            return npc.BaseId == baseId && native->CompanionOwnerId == companionOwnerId;
        });

        if (foundNpc.IsDefault())
        {
            foundNpc = allNpcs.FirstOrDefault((npc) =>
            {
                return npc.BaseId == baseId &&
                       Vector3.Distance(npc.Position, position) < 0.3f;
            });
        }

        if (foundNpc == default)
        {
            NoireLogger.LogDebug($"Could not find mannequin NPC for BaseId {baseId} at position {position}");
            return;
        }

        NoireLogger.LogDebug($"Found mannequin NPC for BaseId {baseId} at position {position}: Name={foundNpc.Name}");

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

        MoveToUntilInReach(foundNpc);
        InteractWithTarget();
        OpenMannequinMerchantSetting();

        foreach (var (slotType, mannequinSlot) in mannequin.Slots)
        {
            EnsureSlotEmpty(slotType, mannequinSlot);
            SetSlotOnMannequin(slotType, mannequinSlot);
        }

        CloseMannequinMerchantSetting();

        TaskBuilder.AddAction(() => Service.TextAdvanceIPC.DisableExternalControl(), Service.TaskQueue, "Disable TextAdvance");

        if (startQueue)
            Service.TaskQueue.StartQueue();
    }

    public static void MoveToUntilInReach(ICharacter character)
    {
        TaskBuilder.Create("Waiting to be available")
            .WithCondition(task => !NoireService.Condition.Any(ConditionFlag.OccupiedInEvent, ConditionFlag.OccupiedInQuestEvent))
            .WithTimeout(TimeSpan.FromSeconds(5))
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);

        TaskBuilder.Create("Target and move to Mannequin")
            .WithAction(task =>
            {
                NoireService.TargetManager.SetTarget(character);
                Service.LifestreamIPC.Move([character.Position]);
            })
            .WithCondition(task =>
            {
                if (NoireService.ClientState.LocalPlayer == null)
                    return false;

                if (Vector3.Distance(NoireService.ClientState.LocalPlayer.Position, character.Position) < 3.5f)
                {
                    Service.LifestreamIPC.Move([]);
                    return true;
                }

                return false;
            })
            .WithTimeout(TimeSpan.FromSeconds(15))
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);
    }

    public static unsafe void InteractWithTarget()
    {
        TaskBuilder.Create("Interact with Target")
            .WithCondition(task =>
            {
                var target = NoireService.TargetManager.Target;

                if (target == null)
                    return false;

                TargetSystem.Instance()->InteractWithObject(target.Struct(), false);
                return true;
            })
            .WithRetries(3, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5))
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);
    }

    public static unsafe void OpenMannequinMerchantSetting()
    {
        TaskBuilder.Create("Open Mannequin Merchant Setting")
            .WithCondition(task =>
            {
                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) || !GenericHelpers.IsAddonReady(addon))
                    return false;

                Callback.Fire(addon, true, 0);
                return true;
            })
            .WithTimeout(TimeSpan.FromSeconds(5))
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);
    }

    private static unsafe void EnsureSlotEmpty(SlotType slotType, MannequinSlot mannequinSlot)
    {
        var finalPriceOfSlot = CommonHelper.GetFinalPriceOfSlot(slotType, mannequinSlot);

        if (finalPriceOfSlot == null)
        {
            NoireLogger.LogDebug($"Skipping slot {slotType} for {mannequinSlot.AssignedCharacter?.UniqueId}, not enough gils or no item configured.");
            return;
        }

        var slotNumber = (uint)slotType;

        TaskBuilder.Create("Wait for MerchantSetting Ready")
            .WithCondition(task =>
            {
                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("MerchantSetting", out var addon) || !GenericHelpers.IsAddonReady(addon))
                    return false;

                Callback.Fire(addon, true, 13, slotNumber);
                return true;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.Create($"Try to open context menu for slot {slotNumber} for {mannequinSlot.AssignedCharacter!.UniqueId}")
            .WithCondition(task =>
            {
                if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("ContextMenu", out var contextMenuAddon) && GenericHelpers.IsAddonReady(contextMenuAddon))
                {
                    // Context menu is open, meaning the slot has an item
                    task.Metadata = true;
                    return true;
                }
                task.Metadata = null;
                return false;
            })
            .WithTimeout(TimeSpan.FromSeconds(1))
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.Create($"Remove item from slot {slotNumber}")
            .WithCondition(task =>
            {
                var previousTaskMetadata = TaskBuilder.GetMetadataFromTask<object?>(Service.TaskQueue, $"Try to open context menu for slot {slotNumber} for {mannequinSlot.AssignedCharacter!.UniqueId}");

                if (previousTaskMetadata == null)
                    return true;

                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("ContextMenu", out var addon) || !GenericHelpers.IsAddonReady(addon))
                    return false;

                Callback.Fire(addon, true, 0, 0, 0);
                return true;
            })
            .WithTimeout(TimeSpan.FromSeconds(5))
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);

        // When the slot on the mannequin is already bought (sold out) this task is always stalling because the SelectYesno never has to appear
        TaskBuilder.Create($"Select yes")
            .WithCondition(task =>
            {
                var previousTaskMetadata = TaskBuilder.GetMetadataFromTask<object?>(Service.TaskQueue, $"Try to open context menu for slot {slotNumber} for {mannequinSlot.AssignedCharacter!.UniqueId}");

                if (previousTaskMetadata == null)
                    return true;

                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("SelectYesno", out var addon) || !GenericHelpers.IsAddonReady(addon))
                    return false;

                NoireLogger.LogDebug($"Confirming Fire callback SelectYesno");

                Callback.Fire(addon, true, 0);
                return true;
            })
            .WithTimeout(TimeSpan.FromSeconds(2))
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);

        TaskBuilder.Create($"Confirm removal for slot {slotNumber}")
            .WithCondition(task =>
            {
                var previousTaskMetadata = TaskBuilder.GetMetadataFromTask<object?>(Service.TaskQueue, $"Try to open context menu for slot {slotNumber} for {mannequinSlot.AssignedCharacter!.UniqueId}");

                if (previousTaskMetadata == null)
                    return true;

                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("SelectYesno", out var addon) || !GenericHelpers.IsAddonReady(addon))
                {
                    return true;
                }
                return false;
            })
            .WithTimeout(TimeSpan.FromSeconds(5))
            .EnqueueTo(Service.TaskQueue);
    }

    private static unsafe void SetSlotOnMannequin(SlotType slotType, MannequinSlot mannequinSlot)
    {
        var finalPriceOfSlot = CommonHelper.GetFinalPriceOfSlot(slotType, mannequinSlot);
        if (finalPriceOfSlot == null)
        {
            NoireLogger.LogDebug($"Skipping slot {slotType} for {mannequinSlot.AssignedCharacter?.UniqueId}, not enough gils or no item configured.");
            return;
        }

        var slotNumber = (uint)slotType;

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);

        TaskBuilder.Create("Open Select Item Menu")
            .WithCondition(task =>
            {
                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("MerchantSetting", out var addon) || !GenericHelpers.IsAddonReady(addon))
                    return false;

                Callback.Fire(addon, true, 12, slotNumber);
                return true;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);

        TaskBuilder.Create("Find Right Item")
            .WithCondition(task =>
            {
                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("MerchantEquipSelect", out var addon) || !GenericHelpers.IsAddonReady(addon))
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
                            NoireLogger.LogDebug($"Found item with text: {text}");

                            if (itemSheet.TryGetRow(Configuration.Instance.ItemsPerSlot[slotType], out var item))
                            {
                                NoireLogger.LogDebug($"Found item with Id: {item.RowId}, name: {item.Name}, Is same ? {item.Name == text}");
                                if (item.Name == text)
                                {
                                    task.Metadata = nodeIndex == 4 ? 0 : nodeIndex - 41001;
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            })
            .WithTimeout(TimeSpan.FromSeconds(10))
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.Create("Select Item In List To Equip Slot")
            .WithCondition(task =>
            {
                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("MerchantEquipSelect", out var addon) || !GenericHelpers.IsAddonReady(addon))
                    return false;

                var previousMetadata = TaskBuilder.GetMetadataFromTask<object?>(Service.TaskQueue, "Find Right Item");
                if (previousMetadata == null || previousMetadata is not int nodeIndex)
                    return false;

                Callback.Fire(addon, true, 19, nodeIndex);
                return true;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);

        TaskBuilder.Create("Get RetainerSell Addon")
            .WithCondition(task =>
            {
                if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("RetainerSell", out var addon) && GenericHelpers.IsAddonReady(addon))
                {
                    Callback.Fire(addon, true, 2, (int)finalPriceOfSlot);
                    task.Metadata = new PointerMetadata<AtkUnitBase>(addon);
                    return true;
                }
                return false;
            })
            .WithTimeout(TimeSpan.FromSeconds(5))
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);

        TaskBuilder.Create("Confirm Selling Item")
            .WithCondition(task =>
            {
                if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("RetainerSell", out var addon) && GenericHelpers.IsAddonReady(addon))
                {
                    Callback.Fire(addon, true, 0);
                    return true;
                }
                return false;
            })
            .WithTimeout(TimeSpan.FromSeconds(5))
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);
    }

    private static unsafe void CloseMannequinMerchantSetting()
    {
        TaskBuilder.Create("Close Mannequin Merchant Setting")
            .WithCondition(task =>
            {
                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("MerchantSetting", out var addon) || !GenericHelpers.IsAddonReady(addon))
                    return false;

                Callback.Fire(addon, true, 11, 0);
                return true;
            })
            .WithTimeout(TimeSpan.FromSeconds(5))
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.Create("Close Mannequin Merchant Setting")
            .WithCondition(task =>
            {
                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) || !GenericHelpers.IsAddonReady(addon))
                    return false;

                Callback.Fire(addon, true, 6);
                return true;
            })
            .WithTimeout(TimeSpan.FromSeconds(5))
            .EnqueueTo(Service.TaskQueue);
    }
}
