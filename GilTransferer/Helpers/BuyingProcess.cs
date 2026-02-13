using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Utility;
using ECommons;
using ECommons.DalamudServices.Legacy;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game;
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

/// <summary>
/// Provides the necessary to process character purchases for mannequins in the game.
/// </summary>
public static class BuyingProcess
{
    private sealed record PendingCharacterPurchase(Scenario Scenario, Mannequin Mannequin, MannequinSlot MannequinSlot);

    private static readonly Queue<PendingCharacterPurchase> PendingPurchases = new();
    private static string? _pendingScenarioId;

    /// <summary>
    ///  Processes all character purchases for the given scenario.
    /// </summary>
    public static void ProcessAllCharacterPurchases(Scenario? scenario)
    {
        if (scenario == null || Service.LifestreamIPC.IsBusy())
            return;

        if (_pendingScenarioId != scenario.UniqueID || PendingPurchases.Count == 0)
        {
            PendingPurchases.Clear();
            _pendingScenarioId = scenario.UniqueID;

            foreach (var mannequin in scenario.Mannequins)
            {
                foreach (var (slotType, mannequinSlot) in mannequin.Slots)
                {
                    if (mannequinSlot.AssignedCharacter == null)
                        continue;

                    var finalPriceSlot = CommonHelper.GetFinalPriceOfSlot(slotType, mannequinSlot, scenario);
                    if (finalPriceSlot == null)
                        continue;

                    PendingPurchases.Enqueue(new PendingCharacterPurchase(scenario, mannequin, mannequinSlot));
                }
            }
        }

        EnqueueNextCharacterPurchase(startQueue: true);
    }

    private static void EnqueueNextCharacterPurchase(bool startQueue)
    {
        if (PendingPurchases.Count == 0)
        {
            _pendingScenarioId = null;
            return;
        }

        var nextPurchase = PendingPurchases.Dequeue();
        ProcessCharacterPurchase(nextPurchase.Scenario, nextPurchase.Mannequin, nextPurchase.MannequinSlot);

        // Add a task to process the next purchase after the current one is done, if there are more purchases in the queue
        TaskBuilder.AddAction(() =>
        {
            if (PendingPurchases.Count == 0)
            {
                _pendingScenarioId = null;
                return;
            }

            EnqueueNextCharacterPurchase(false);
        }, Service.TaskQueue, "Queue next character purchase");

        if (startQueue)
            Service.TaskQueue.StartQueue();
    }

    public static void SkipCurrentCharacterPurchase()
    {
        Service.StopQueue();
        EnqueueNextCharacterPurchase(true);
    }

    /// <summary>
    /// Automates the process of purchasing an item from a mannequin for a specified character.
    /// </summary>
    // could make it so that it can process multiple slots at once per character
    private static unsafe void ProcessCharacterPurchase(Scenario scenario, Mannequin mannequin, MannequinSlot mannequinSlot)
    {
        var assignedCharacter = mannequinSlot.AssignedCharacter!;
        var playerForEstateTp = mannequin.PlayerForEstateTPOverride ?? scenario.DefaultPlayerForEstateTP;

        TaskBuilder.Create($"Login to {assignedCharacter!.FullName}")
            .WithAction(task =>
            {
                var localPlayer = NoireService.ObjectTable.LocalPlayer;
                if (localPlayer != null && localPlayer.Name.TextValue == assignedCharacter.PlayerName &&
                    localPlayer.HomeWorld.Value.Name.ExtractText() == assignedCharacter.Homeworld)
                    return;

                var errorCode = Service.LifestreamIPC.ChangeCharacter(assignedCharacter.PlayerName, assignedCharacter.Homeworld);
                if (errorCode != ErrorCode.Success)
                    throw new Exception($"Failed to change character: {errorCode}");
            })
            .OnFailedOrCancelled((task, ex) =>
            {
                SkipCurrentCharacterPurchase();
            })
            .WithCondition(task =>
            {
                var localPlayer = NoireService.ObjectTable.LocalPlayer;

                if (localPlayer == null)
                    return false;

                bool isCorrectPlayer = localPlayer.Name.TextValue == assignedCharacter.PlayerName &&
                       localPlayer.HomeWorld.Value.Name.ExtractText() == assignedCharacter.Homeworld;

                bool occupied = CommonHelper.IsOccupied();

                return isCorrectPlayer && !occupied;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(2000, Service.TaskQueue);

        TaskBuilder.Create($"Moving to world {playerForEstateTp.Homeworld}")
            .WithAction(task =>
            {
                var localPlayer = NoireService.ObjectTable.LocalPlayer;
                if (localPlayer != null && localPlayer.CurrentWorld.Value.Name.ExtractText() == playerForEstateTp.Homeworld)
                    return;

                Service.LifestreamIPC.ChangeWorld(playerForEstateTp.Homeworld);
            })
            .WithCondition(task =>
            {
                var localPlayer = NoireService.ObjectTable.LocalPlayer;

                if (localPlayer == null)
                    return false;

                bool correctWorld = localPlayer.CurrentWorld.Value.Name.ExtractText() == playerForEstateTp.Homeworld;
                bool occupied = NoireService.Condition.Any(ConditionFlag.BetweenAreas);

                return correctWorld && !occupied;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(2000, Service.TaskQueue);

        TaskBuilder.Create($"Opening estate list")
            .WithAction(task =>
            {
                CommonHelper.OpenEstateList(playerForEstateTp.PlayerName);
            })
            .WithCondition(task =>
            {
                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("TeleportHousingFriend", out var addon) || !GenericHelpers.IsAddonReady(addon))
                    return false;

                return true;
            })
            .WithRetries(2, TimeSpan.FromSeconds(3))
            .OnMaxRetriesExceeded(task =>
            {
                SkipCurrentCharacterPurchase();
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);

        TaskBuilder.Create($"Getting Estate Tp for {assignedCharacter.UniqueId}")
            .WithCondition(task =>
            {
                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("TeleportHousingFriend", out var addon) || !GenericHelpers.IsAddonReady(addon))
                    return false;

                List<int> nodeIndexes = [2];

                for (int i = 21001; i < 21020; i++) // 20 ? To be sure it's been found
                {
                    nodeIndexes.Add(i);
                }

                foreach (var nodeIndex in nodeIndexes)
                {
                    var node = GenericHelpers.GetNodeByIDChain(addon->RootNode, 1, 6, 8, nodeIndex, 5);

                    if (node != null)
                    {
                        var nodeText = node->GetAsAtkTextNode()->NodeText.GetText();
                        if (!nodeText.IsNullOrWhitespace())
                        {
                            if (nodeText == "Free Company Estate" && (mannequin.DestinationType == DestinationType.FreeCompany || mannequin.DestinationType == DestinationType.FCChamber))
                            {
                                Callback.Fire(addon, true, 0); // Callback for FC is 0
                                return true;
                            }
                            else if (nodeText == "Apartments" && mannequin.DestinationType == DestinationType.Apartment)
                            {
                                Callback.Fire(addon, true, 2); // Callback for apartment is 2
                                return true;
                            }
                            else if (nodeText == "Private Estate" && mannequin.DestinationType == DestinationType.Private)
                            {
                                Callback.Fire(addon, true, 1); // Callback for private estate is 1
                                return true;
                            }
                        }
                    }
                }

                return false;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(2000, Service.TaskQueue);

        TaskBuilder.Create("Waiting to be available")
            .WithCondition(task =>
            {
                var localPlayer = NoireService.ObjectTable.LocalPlayer;

                if (localPlayer == null)
                    return false;

                bool occupied = CommonHelper.IsOccupied();

                if (occupied)
                    return false;

                var territoryType = NoireService.ClientState.TerritoryType;
                var placeNameId = ExcelSheetHelper.GetSheet<TerritoryType>()!.GetRow(territoryType)!.PlaceNameZone.Value.RowId;

                if (placeNameId != mannequin.PlaceNameId)
                    return false;

                var housingManager = HousingManager.Instance();
                var ward = housingManager->GetCurrentWard();

                if (ward != mannequin.Ward)
                    return false;

                return true;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);

        // Initially to check if in the right plot, but this check has been removed
        // Now it's used as a "security" to make sure the closest door is the one in the plot I guess?
        TaskBuilder.Create("Moving forward a bit")
            .WithAction(CommonHelper.MoveForward)
            .WithCondition(() => !Service.LifestreamIPC.IsBusy())
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.Create("Find House/Apartment Entrance & Enter")
            .WithCondition(() =>
            {
                var localPlayer = NoireService.ObjectTable.LocalPlayer;

                if (localPlayer == null)
                    return false;

                var foundEntrance = NoireService.ObjectTable.OrderBy(x => Vector3.Distance(x.Position, localPlayer.Position))
                    .First(x =>
                    {
                        var distance = Vector3.Distance(x.Position, localPlayer.Position);
                        bool foundEntrance = false;

                        if (mannequin.DestinationType == DestinationType.Apartment)
                            foundEntrance = x.BaseId == (uint)EntranceType.ApartmentEntrance;
                        else
                            foundEntrance = x.BaseId == (uint)EntranceType.EstateEntrance;

                        return distance < 20 && foundEntrance;
                    });

                if (foundEntrance == null)
                    return false;

                if (!foundEntrance.IsTarget())
                    NoireService.TargetManager.SetTarget(foundEntrance);

                // Could use LifestreamIPC.Move but lifestream itself does it like this and i'm a shameless follower
                ChatHelper.SendMessage("/lockon");
                ChatHelper.SendMessage("/automove on");

                return true;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);

        TaskBuilder.Create("Waiting to be near door")
            .WithCondition(() =>
            {
                var localPlayer = NoireService.ObjectTable.LocalPlayer;

                if (localPlayer == null)
                    return false;

                var target = NoireService.TargetManager.Target;
                return target == null || Vector3.Distance(target.Position, localPlayer.Position) < 3.5f;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);

        if (mannequin.DestinationType == DestinationType.Apartment)
        {
            // TODO : Process apartment
            return;
        }
        else
        {
            TaskBuilder.Create("Enter House")
                .WithAction(() =>
                {
                    ChatHelper.SendMessage("/automove off");

                    // Check again if right ward and right plot
                    var housingManager = HousingManager.Instance();
                    var ward = housingManager->GetCurrentWard();
                    var plot = housingManager->GetCurrentPlot() + 1;

                    if (ward != mannequin.Ward || plot != mannequin.Plot)
                    {
                        SkipCurrentCharacterPurchase();
                        return;
                    }

                    var target = NoireService.TargetManager.Target;
                    TargetSystem.Instance()->InteractWithObject(target.Struct(), false);
                })
                .WithCondition(() =>
                {
                    var localPlayer = NoireService.ObjectTable.LocalPlayer;

                    if (localPlayer == null)
                        return false;

                    bool occupied = CommonHelper.IsOccupied();

                    if (occupied)
                        return false;

                    var housingManager = HousingManager.Instance();
                    var isInside = housingManager->IsInside();

                    if (!isInside)
                        return false;

                    var foundEntrance = NoireService.ObjectTable.FirstOrDefault(x => CommonHelper.IsAnyWorkshopEntrance(x.BaseId));

                    return foundEntrance != default;
                })
                .EnqueueTo(Service.TaskQueue);

            TaskBuilder.AddDelayMilliseconds(2000, Service.TaskQueue);
        }

        if (mannequin.DestinationType == DestinationType.FCChamber)
        {
            ProcessFCChamber(mannequin);
        }
        else if (mannequin.DestinationType == DestinationType.Apartment)
        {
            // TODO: Process Apartment
            return;
        }
        else if (mannequin.DestinationType == DestinationType.Private || mannequin.DestinationType == DestinationType.FreeCompany)
        {
            // Do nothing specific, we are already in the right place at that point
        }

        TaskBuilder.Create($"Target and move to Mannequin {mannequin.UniqueId}")
            .WithAction(task =>
            {
                var allNpcs = NoireService.ObjectTable.OfType<INpc>();
                var foundNpc = allNpcs.FirstOrDefault((npc) =>
                {
                    var native = CharacterHelper.GetCharacterAddress(npc);
                    return npc.BaseId == mannequin.BaseId && native->CompanionOwnerId == mannequin.CompanionOwnerId;
                });

                task.Metadata = foundNpc;

                NoireService.TargetManager.SetTarget(foundNpc!);
                Service.LifestreamIPC.Move([foundNpc!.Position]);
            })
            .WithCondition(task =>
            {
                var npc = task.Metadata as INpc;

                if (NoireService.ObjectTable.LocalPlayer == null || npc == null)
                    return false;

                if (Vector3.Distance(NoireService.ObjectTable.LocalPlayer.Position, npc.Position) < 3.5f)
                {
                    Service.LifestreamIPC.Move([]);
                    return true;
                }

                return false;
            })
            .WithTimeout(TimeSpan.FromSeconds(15))
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(1000, Service.TaskQueue);

        TaskBuilder.Create("Select Right Slot")
            .WithAction(() =>
            {
                var target = NoireService.TargetManager.Target;
                if (target != null && target.BaseId == mannequin.BaseId)
                    TargetSystem.Instance()->InteractWithObject(target.Struct(), false);
            })
            .WithCondition(task =>
            {
                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("MerchantShop", out var addon) || !GenericHelpers.IsAddonReady(addon))
                    return false;
                return true;
            })
            .WithRetries(5, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1))
            .WithRetryAction(() =>
            {
                var target = NoireService.TargetManager.Target;

                if (target == null)
                {
                    var npc = TaskBuilder.GetMetadataFromTask<INpc>(Service.TaskQueue, $"Target and move to Mannequin {mannequin.UniqueId}");

                    if (npc == null)
                        return;

                    NoireService.TargetManager.SetTarget(npc);
                }

                if (target != null && target.BaseId == mannequin.BaseId)
                    TargetSystem.Instance()->InteractWithObject(target.Struct(), false);
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);

        TaskBuilder.Create("Click the Right Slot")
            .WithCondition(task =>
            {
                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("MerchantShop", out var addon) || !GenericHelpers.IsAddonReady(addon))
                    return false;

                var mask = CommonHelper.SlotsToMask([(int)mannequinSlot.SlotType]);
                Callback.Fire(addon, true, 15, mask);
                return true;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);

        TaskBuilder.Create("Press the purchase button")
            .WithCondition(task =>
            {
                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("MerchantShop", out var addon) || !GenericHelpers.IsAddonReady(addon))
                    return false;

                Callback.Fire(addon, true, 14, 0);
                return true;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);

        TaskBuilder.Create("Confirm the purchase")
            .WithCondition(task =>
            {
                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("SelectYesno", out var addon) || !GenericHelpers.IsAddonReady(addon))
                    return false;

                Callback.Fire(addon, true, 0);
                return true;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);
    }

    private unsafe static void ProcessFCChamber(Mannequin mannequin)
    {
        TaskBuilder.Create("Target and move to workshop entrance")
            .WithCondition(task =>
            {
                var localPlayer = NoireService.ObjectTable.LocalPlayer;
                if (localPlayer == null)
                    return false;

                var foundEntrance = NoireService.ObjectTable.FirstOrDefault(x => CommonHelper.IsAnyWorkshopEntrance(x.BaseId));
                if (foundEntrance == default)
                    return false;

                if (!foundEntrance.IsTarget())
                    NoireService.TargetManager.SetTarget(foundEntrance);

                ChatHelper.SendMessage("/lockon");
                ChatHelper.SendMessage("/automove on");
                return true;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.Create("Waiting to be within reach")
            .WithCondition(task =>
            {
                var localPlayer = NoireService.ObjectTable.LocalPlayer;
                if (localPlayer == null)
                    return false;

                var occupied = CommonHelper.IsOccupied();

                var target = NoireService.TargetManager.Target ?? null;
                return target != null && Vector3.Distance(target.Position, localPlayer.Position) < 3.5f && !occupied;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.Create("Interact with door")
            .WithAction(() =>
            {
                ChatHelper.SendMessage("/automove off");

                var target = NoireService.TargetManager.Target ?? null;
                if (target != null && CommonHelper.IsAnyWorkshopEntrance(target.BaseId))
                    TargetSystem.Instance()->InteractWithObject(target.Struct(), false);
            })
            .WithCondition(task =>
            {
                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) || !GenericHelpers.IsAddonReady(addon))
                    return false;

                List<int> nodeIndexes = [5];

                for (int i = 51001; i < 51020; i++) // 20 ? To be sure it's been found
                {
                    nodeIndexes.Add(i);
                }

                foreach (var nodeIndex in nodeIndexes)
                {
                    var node = GenericHelpers.GetNodeByIDChain(addon->RootNode, 1, 3, nodeIndex, 2);

                    if (node != null)
                    {
                        var nodeText = node->GetAsAtkTextNode()->NodeText.GetText();
                        if (!nodeText.IsNullOrWhitespace())
                        {
                            if (nodeText == "Move to specified private chambers")
                            {
                                Callback.Fire(addon, true, (nodeIndex == 5 ? 0 : nodeIndex - 51000));
                                return true;
                            }
                        }
                    }
                }

                return false;
            })
            .WithRetries(5, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1))
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);

        TaskBuilder.Create("Select room range on the left")
            .WithCondition(task =>
            {
                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("HousingSelectRoom", out var addon) || !GenericHelpers.IsAddonReady(addon))
                    return false;

                double room = mannequin.ChamberOrApartmentNumber;
                var roomRangeIndex = Math.Floor((room - 1) / 15);

                Callback.Fire(addon, true, 1, (int)roomRangeIndex);
                return true;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);

        TaskBuilder.Create("Select room range on the left")
            .WithCondition(task =>
            {
                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("HousingSelectRoom", out var addon) || !GenericHelpers.IsAddonReady(addon))
                    return false;

                double room = mannequin.ChamberOrApartmentNumber;
                var roomIndex = (room - 1) % 15;

                Callback.Fire(addon, true, 0, (int)roomIndex);
                return true;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);

        TaskBuilder.Create("Select the room on the right and enter it")
            .WithCondition(task =>
            {
                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("SelectYesno", out var addon) || !GenericHelpers.IsAddonReady(addon))
                    return false;

                Callback.Fire(addon, true, 0);
                return true;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);

        TaskBuilder.Create("Waiting to be inside the FC room")
            .WithCondition(task =>
            {
                var localPlayer = NoireService.ObjectTable.LocalPlayer;

                if (localPlayer == null)
                    return false;

                bool occupied = CommonHelper.IsOccupied();

                var allNpcs = NoireService.ObjectTable.OfType<INpc>();
                var foundNpc = allNpcs.FirstOrDefault((npc) =>
                {
                    var native = CharacterHelper.GetCharacterAddress(npc);
                    return npc.BaseId == mannequin.BaseId && native->CompanionOwnerId == mannequin.CompanionOwnerId;
                });

                if (foundNpc.IsDefault())
                {
                    foundNpc = allNpcs.FirstOrDefault((npc) =>
                    {
                        return npc.BaseId == mannequin.BaseId &&
                               Vector3.Distance(npc.Position, mannequin.Position) < 0.3f;
                    });
                }

                if (foundNpc == default)
                    return false;

                return !occupied;
            })
            .EnqueueTo(Service.TaskQueue);

        TaskBuilder.AddDelayMilliseconds(500, Service.TaskQueue);
    }
}
