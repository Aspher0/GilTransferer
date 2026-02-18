using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Memory;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using GilTransferer.Enums;
using GilTransferer.Models;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace GilTransferer.Helpers;

public static class CommonHelper
{
    /// <summary>
    /// Checks if a character (by name and world) is already assigned to any slot in any mannequin
    /// </summary>
    public static bool IsCharacterAssignedAnywhere(Scenario scenario, string characterName, string characterWorld, out string assignmentInfo)
    {
        assignmentInfo = string.Empty;

        if (scenario == null)
            return false;

        for (int i = 0; i < scenario.Mannequins.Count; i++)
        {
            var mannequin = scenario.Mannequins[i];
            foreach (var (slotType, mannequinSlot) in mannequin.Slots)
            {
                var slot = mannequinSlot;
                if (slot.AssignedCharacter != null &&
                    slot.AssignedCharacter.PlayerName == characterName &&
                    slot.AssignedCharacter.Homeworld == characterWorld)
                {
                    assignmentInfo = $"Mannequin #{i + 1}, {slotType}";
                    return true;
                }
            }
        }

        return false;
    }

    public static bool IsOccupied()
    {
        return NoireService.Condition.Any(
            ConditionFlag.Jumping,
            ConditionFlag.Jumping61,
            ConditionFlag.BetweenAreas,
            ConditionFlag.BetweenAreas51,
            ConditionFlag.Casting,
            ConditionFlag.Casting87,
            ConditionFlag.Occupied,
            ConditionFlag.Occupied30,
            ConditionFlag.Occupied33,
            ConditionFlag.Occupied38,
            ConditionFlag.Occupied39,
            ConditionFlag.OccupiedInCutSceneEvent,
            ConditionFlag.OccupiedInEvent,
            ConditionFlag.OccupiedInQuestEvent,
            ConditionFlag.OccupiedSummoningBell
            );
    }

    /// <summary>
    /// Gets the slot type corresponding to the specified equipment slot category identifier from the Item excel sheet.<br/>
    /// Warning, RingLeft will default to RingRight.
    /// </summary>
    public static SlotType? GetSlotTypeFromEquipSlotCategory(uint equipSlotCategory)
    {
        switch (equipSlotCategory)
        {
            case 1:
            case 13:
                return SlotType.MainHand;
            case 2:
                return SlotType.OffHand;
            case 3:
                return SlotType.Head;
            case 4:
            case 15:
            case 16:
            case 19:
            case 20:
            case 21:
            case 22:
            case 23:
                return SlotType.Body;
            case 5:
                return SlotType.Hands;
            case 7:
            case 18:
                return SlotType.Legs;
            case 8:
                return SlotType.Feet;
            case 9:
                return SlotType.Ears;
            case 10:
                return SlotType.Neck;
            case 11:
                return SlotType.Wrists;
            case 12:
                return SlotType.RingRight; // Also RingLeft, but we default to RingRight
            default:
                return null;
        }
    }

    /// <summary>
    /// Moves forward a bit using <see cref="GetMoveForwardVector"/>
    /// </summary>
    public static void MoveForward()
    {
        var newPosition = GetMoveForwardVector();
        Service.LifestreamIPC.Move([newPosition]);
    }

    /// <summary>
    /// Gets a position vector a few units in front of the local player
    /// </summary>
    public static Vector3 GetMoveForwardVector()
    {
        var localPlayer = NoireService.ObjectTable.LocalPlayer;

        if (localPlayer == null)
            throw new InvalidOperationException("Local player is not loaded.");

        var characterPosition = localPlayer.Position;
        var characterRotation = localPlayer.Rotation;
        var characterForward = ECommons.MathHelpers.MathHelper.GetPointFromAngleAndDistance(characterPosition.ToVector2(), characterRotation, 3);
        Vector3 newPosition = new(characterForward.X, localPlayer.Position.Y, characterForward.Y);

        return newPosition;
    }

    /// <summary>
    /// Used to convert a list of slot indices into a bitmask for buying multiple slots on mannequins
    /// </summary>
    public static int SlotsToMask(IEnumerable<int> slots)
    {
        int mask = 0;
        foreach (int slot in slots)
        {
            mask |= (1 << slot);
        }
        return mask;
    }

    /// <summary>
    /// Calculates the final price of a slot based on the assigned character's gil amount and configuration settings.
    /// </summary>
    public static int? GetFinalPriceOfSlot(SlotType slotType, MannequinSlot mannequinSlot, Scenario selectedScenario)
    {
        var assignedCharacter = mannequinSlot.AssignedCharacter;

        if (assignedCharacter == null || !Configuration.Instance.ItemsPerSlot.ContainsKey(slotType) || !assignedCharacter.ContentId.HasValue)
            return null;

        var data = Service.GetOfflineCharacterData(assignedCharacter.ContentId.Value);
        if (data == null)
            return null;

        var gils = data.Gil;

        if (gils < Math.Max(selectedScenario.GilsToLeaveOnCharacters, selectedScenario.MinGilsToConsiderCharacters))
            return null;

        var finalPriceOfSlot = gils - selectedScenario.GilsToLeaveOnCharacters;
        return (int)finalPriceOfSlot;
    }

    // From SimpleTweaks by Caraxi : https://github.com/Caraxi/SimpleTweaksPlugin/blob/main/Tweaks/EstateListCommand.cs#L18
    public unsafe static bool OpenEstateList(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            return false;

        if (playerName.StartsWith("<") && playerName.EndsWith(">"))
        {
            var resolved = Framework.Instance()->GetUIModule()->GetPronounModule()->ResolvePlaceholder(playerName, 1, 0);
            if (resolved != null)
                playerName = MemoryHelper.ReadStringNullTerminated(new IntPtr(resolved->GetName()));
        }

        var useContentId = ulong.TryParse(playerName, out var contentId);

        var agent = AgentFriendlist.Instance();

        for (var i = 0U; i < agent->InfoProxy->EntryCount; i++)
        {
            var f = agent->InfoProxy->GetEntry(i);
            if (f == null) continue;
            if (f->HomeWorld != NoireService.ObjectTable.LocalPlayer?.CurrentWorld.RowId) continue;
            if (f->ContentId == 0) continue;
            if (f->Name[0] == 0) continue;
            if ((f->ExtraFlags & 32) != 0) continue;
            if (useContentId && contentId == f->ContentId)
            {
                AgentFriendlist.Instance()->OpenFriendEstateTeleportation(f->ContentId);
                return true;
            }

            var name = f->NameString;
            if (name.StartsWith(playerName, StringComparison.InvariantCultureIgnoreCase))
            {
                AgentFriendlist.Instance()->OpenFriendEstateTeleportation(f->ContentId);
                return true;
            }
        }

        return false;
    }

    public static bool IsAnyWorkshopEntrance(uint baseId)
    {
        return baseId == (uint)EntranceType.WorkshopEntranceMistAndLB ||
               baseId == (uint)EntranceType.WorkshopEntranceUldah ||
               baseId == (uint)EntranceType.WorkshopEntranceShirogane ||
               baseId == (uint)EntranceType.WorkshopEntranceEmpyreum;
    }

    public static unsafe INpc? FindMannequinNpc(Mannequin mannequin)
    {
        var allNpcs = NoireService.ObjectTable.OfType<INpc>().Where(npc => npc.ObjectKind == ObjectKind.EventNpc);
        return FindMannequinNpc(mannequin, allNpcs);
    }

    public static unsafe INpc? FindMannequinNpc(Mannequin mannequin, IEnumerable<INpc> npcs)
    {
        var foundNpc = npcs.FirstOrDefault(npc =>
        {
            return mannequin.Equals(MakeMannequin(npc));
        });

        if (foundNpc == null)
            foundNpc = npcs.FirstOrDefault(npc => mannequin.Equals(MakeMannequin(npc), true));

        return foundNpc;
    }

    public static unsafe Mannequin MakeMannequin(INpc mannequin)
    {
        var npcNative = CharacterHelper.GetCharacterAddress(mannequin);
        var targetBaseId = mannequin.BaseId;
        var targetCompanionOwnerId = npcNative->CompanionOwnerId;

        var housingManager = HousingManager.Instance();
        var ward = housingManager->GetCurrentWard() + 1;
        var plot = housingManager->GetCurrentPlot() + 1; // Plot is 0 indexed in the struct but 1 indexed for users, so we add 1 here. Also, -1 means we're not on a plot, so it now becomes 0 if not on plot.
        var room = housingManager->GetCurrentRoom();
        var houseId = housingManager->GetCurrentIndoorHouseId();

        var territoryType = NoireService.ClientState.TerritoryType;
        var placeNameId = ExcelSheetHelper.GetSheet<TerritoryType>()!.GetRow(territoryType)!.PlaceNameZone.Value.RowId;

        DestinationType destinationType = DestinationType.Unknown;

        if (room != 0)
            destinationType = houseId.IsApartment ? DestinationType.Apartment : DestinationType.FCChamber;
        else
        {
            var foundEntrance = NoireService.ObjectTable.FirstOrDefault(x => CommonHelper.IsAnyWorkshopEntrance(x.BaseId));

            // Todo: Find a better way, if the FC has no workshop nor rooms, the door MIGHT NOT be interactable and the object might not be set in the object table
            if (foundEntrance == null)
                destinationType = DestinationType.Private;
            else
                destinationType = DestinationType.FreeCompany;
        }

        return new Mannequin(null, targetBaseId, targetCompanionOwnerId, mannequin.Position, destinationType, placeNameId, ward, plot, room);
    }
}
