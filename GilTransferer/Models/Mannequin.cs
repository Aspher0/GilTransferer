using NoireLib.Helpers;
using System.Collections.Generic;
using System.Numerics;
using System;
using System.Text.Json.Serialization;
using GilTransferer.Enums;

namespace GilTransferer.Models;

/// <summary>
/// A class representing a mannequin used for transferring gils.
/// </summary>
[Serializable]
public class Mannequin
{
    /// <summary>
    /// A unique identifier for this Mannequin instance.
    /// </summary>
    public string UniqueId { get; set; } = RandomGenerator.GenerateGuidString();

    /// <summary>
    /// The identifier for the mannequin.<br/>
    /// Multiple mannequins can share the same BaseId if they are associated to the same retainer.
    /// </summary>
    public uint BaseId { get; set; }

    /// <summary>
    /// For differentiating between mannequins.<br/>
    /// For Mannequins in an indoor housing area, this is an index value to IndoorTerritory.FurnitureManager.FurnitureMemory
    /// </summary>
    public uint CompanionOwnerId { get; set; }

    /// <summary>
    /// The known position of the mannequin in the game world.<br/>
    /// Used as a way to help identify the mannequin in specific scenarios.
    /// </summary>
    public Vector3 Position { get; set; }

    /// <summary>
    /// Represents a mapping of slot numbers to the corresponding PlayerModel characters assigned to those slots on the mannequin.<br/>
    /// Used to tell which character is assigned to which slot for gil transfer purposes.
    /// </summary>
    public Dictionary<SlotType, MannequinSlot> Slots { get; set; } = new();

    [JsonConstructor]
    public Mannequin(string? uniqueId, uint baseId, uint companionOwnerId, Vector3 position)
    {
        if (uniqueId != null)
            UniqueId = uniqueId;

        BaseId = baseId;
        CompanionOwnerId = companionOwnerId;
        Position = position;

        // Initialize all slots
        foreach (SlotType slotType in Enum.GetValues<SlotType>())
        {
            if (!Slots.ContainsKey(slotType))
            {
                Slots[slotType] = new MannequinSlot(slotType);
            }
        }
    }
}
