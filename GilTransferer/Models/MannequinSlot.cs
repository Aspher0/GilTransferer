using GilTransferer.Enums;
using NoireLib.Models;
using System;
using System.Text.Json.Serialization;

namespace GilTransferer.Models;

/// <summary>
/// Represents a slot on a mannequin used for gil transfer.
/// </summary>
[Serializable]
public class MannequinSlot
{
    /// <summary>
    /// The type of slot on the mannequin.
    /// </summary>
    public SlotType SlotType { get; set; }

    /// <summary>
    /// The character that will buy this slot.
    /// </summary>
    public PlayerModel? AssignedCharacter { get; set; } = null;

    /// <summary>
    /// The price in gils for this slot.
    /// </summary>
    //public int GilsPrice { get; set; } = 0;

    [JsonConstructor]
    public MannequinSlot(SlotType slotType, PlayerModel? assignedCharacter = null)
    {
        SlotType = slotType;
        AssignedCharacter = assignedCharacter;
        //GilsPrice = gilsPrice;
    }
}
