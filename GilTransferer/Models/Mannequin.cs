using GilTransferer.Enums;
using NoireLib.Helpers;
using NoireLib.Models;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;

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

    // ==========================================================================

    /// <summary>
    /// The type of estate the mannequins are located in.
    /// </summary>
    public DestinationType DestinationType { get; set; }

    /// <summary>
    /// The https://exd.camora.dev/sheet/PlaceName ID of the destination estate.
    /// </summary>
    public uint PlaceNameId { get; set; }

    /// <summary>
    /// The ward number of the destination estate.
    /// </summary>
    public int Ward { get; set; }

    /// <summary>
    /// The plot number of the destination estate.
    /// </summary>
    public int Plot { get; set; }

    /// <summary>
    /// The chamber number of the destination estate (if applicable).
    /// </summary>
    public int ChamberOrApartmentNumber { get; set; } = 1;

    /// <summary>
    /// Represents the player that is in every alt friendlist used for estate teleportation.
    /// </summary>
    public PlayerModel? PlayerForEstateTPOverride { get; set; } = null;

    // ==========================================================================

    /// <summary>
    /// Represents a mapping of slot numbers to the corresponding PlayerModel characters assigned to those slots on the mannequin.<br/>
    /// Used to tell which character is assigned to which slot for gil transfer purposes.
    /// </summary>
    public Dictionary<SlotType, MannequinSlot> Slots { get; set; } = new();

    [JsonConstructor]
    public Mannequin(string? uniqueId,
        uint baseId,
        uint companionOwnerId,
        Vector3 position,
        DestinationType destinationType,
        uint placeNameId,
        int ward,
        int plot,
        int chamberOrApartmentNumber = 1,
        PlayerModel? playerForEstateTPOverride = null)
    {
        if (uniqueId != null)
            UniqueId = uniqueId;

        BaseId = baseId;
        CompanionOwnerId = companionOwnerId;
        Position = position;
        DestinationType = destinationType;
        PlaceNameId = placeNameId;
        Ward = ward;
        Plot = plot;
        ChamberOrApartmentNumber = chamberOrApartmentNumber;
        PlayerForEstateTPOverride = playerForEstateTPOverride;

        // Initialize all slots
        foreach (SlotType slotType in Enum.GetValues<SlotType>())
        {
            if (!Slots.ContainsKey(slotType))
            {
                Slots[slotType] = new MannequinSlot(slotType);
            }
        }
    }

    public bool Equals(Mannequin other, bool ignorePosition = false)
    {
        if (other == null)
            return false;

        return BaseId == other.BaseId &&
               CompanionOwnerId == other.CompanionOwnerId &&
               (ignorePosition || Position == other.Position) &&
               DestinationType == other.DestinationType &&
               PlaceNameId == other.PlaceNameId &&
               Ward == other.Ward &&
               Plot == other.Plot &&
               ChamberOrApartmentNumber == other.ChamberOrApartmentNumber;
    }
}
