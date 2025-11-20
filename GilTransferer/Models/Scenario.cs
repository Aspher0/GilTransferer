using Dalamud.Utility;
using GilTransferer.Enums;
using NoireLib.Helpers;
using NoireLib.Models;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GilTransferer.Models;

/// <summary>
/// Represents a character that will receive gils from other characters.
/// </summary>
[Serializable]
public class Scenario
{
    public string UniqueID { get; private set; } = RandomGenerator.GenerateGuidString();

    /// <summary>
    /// The name of this scenario.
    /// </summary>
    public string ScenarioName { get; set; } = string.Empty;

    /// <summary>
    /// Represents the player that will receive the gils.
    /// </summary>
    // Don't think this is useful anymore, but leaving it for now
    public PlayerModel ReceivingPlayer { get; set; }

    // =============== Might need to move this to Mannequin later ===============

    /// <summary>
    /// Represents the player that is in every alt friendlist used for estate teleportation.
    /// </summary>
    public PlayerModel PlayerForEstateTP { get; set; }

    /// <summary>
    /// The type of estate the mannequins are located in.
    /// </summary>
    public DestinationType DestinationType { get; set; }

    /// <summary>
    /// The territory ID of the destination estate (outside area).
    /// </summary>
    public uint? DestinationOutsideTerritoryId { get; set; } = null;

    /// <summary>
    /// The territory ID of the destination estate (indoor area).
    /// </summary>
    public uint? DestinationIndoorTerritoryId { get; set; } = null;

    /// <summary>
    /// The chamber number of the destination estate (if applicable).
    /// </summary>
    public int ChamberOrApartmentNumber { get; set; } = 1;

    // ==========================================================================

    /// <summary>
    /// A flag indicating whether to show all characters in the combo box, regardless of their gil amount.
    /// </summary>
    public bool ShowAllCharsInComboBox { get; set; } = false;

    /// <summary>
    /// Represents a list of Mannequins that will be used to transfer gils.<br/>
    /// See <see cref="Mannequin"/> for more details.
    /// </summary>
    public List<Mannequin> Mannequins { get; set; } = new();

    [JsonConstructor]
    public Scenario(string uniqueId, string scenarioName, PlayerModel receivingPlayer, DestinationType destinationType = DestinationType.Private)
    {
        UniqueID = uniqueId;
        ScenarioName = scenarioName;
        ReceivingPlayer = receivingPlayer;
        PlayerForEstateTP = receivingPlayer.Clone();
        DestinationType = destinationType;
    }

    public Scenario(PlayerModel receivingPlayer, string? scenarioName = null, DestinationType destinationType = DestinationType.Private)
    {
        if (scenarioName.IsNullOrEmpty())
            ScenarioName = receivingPlayer.FullName;
        else
            ScenarioName = scenarioName;

        ReceivingPlayer = receivingPlayer;
        PlayerForEstateTP = receivingPlayer.Clone();
        DestinationType = destinationType;
    }
}
