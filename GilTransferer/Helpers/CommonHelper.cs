using ECommons.MathHelpers;
using GilTransferer.Enums;
using GilTransferer.Models;
using NoireLib;
using System;
using System.Collections.Generic;
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
        var localPlayer = NoireService.ClientState.LocalPlayer;

        if (localPlayer == null)
            throw new InvalidOperationException("Local player is not loaded.");

        var characterPosition = localPlayer.Position;
        var characterRotation = localPlayer.Rotation;
        var characterForward = MathHelper.GetPointFromAngleAndDistance(characterPosition.ToVector2(), characterRotation, 3);
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
    public static int? GetFinalPriceOfSlot(SlotType slotType, MannequinSlot mannequinSlot)
    {
        var assignedCharacter = mannequinSlot.AssignedCharacter;

        if (assignedCharacter == null || !Configuration.Instance.ItemsPerSlot.ContainsKey(slotType) || !assignedCharacter.ContentId.HasValue)
            return null;

        var data = Service.GetOfflineCharacterData(assignedCharacter.ContentId.Value);
        if (data == null)
            return null;

        var gils = data.Gil;

        if (gils < Math.Max(Configuration.Instance.GilsToLeaveOnCharacters, Configuration.Instance.MinGilsToConsiderCharacters))
            return null;

        var finalPriceOfSlot = gils - Configuration.Instance.GilsToLeaveOnCharacters;
        return (int)finalPriceOfSlot;
    }
}
