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
    /// Represents the player that is in every alt friendlist used for estate teleportation.
    /// </summary>
    public PlayerModel DefaultPlayerForEstateTP { get; set; }

    // ==========================================================================

    /// <summary>
    /// Determines the minimum amount of gils a character must have to be considered for gil transfer.
    /// </summary>
    public int MinGilsToConsiderCharacters { get; set; } = 100000;

    /// <summary>
    /// Determines the approximate amount of gils to keep on each characters (will be a bit less with TP fees).
    /// </summary>
    public int GilsToLeaveOnCharacters { get; set; } = 25000;

    // ==========================================================================

    /// <summary>
    /// Represents a list of Mannequins that will be used to transfer gils.<br/>
    /// See <see cref="Mannequin"/> for more details.
    /// </summary>
    public List<Mannequin> Mannequins { get; set; } = new();



    [JsonConstructor]
    public Scenario(string uniqueId, string scenarioName, PlayerModel defaultPlayerForEstateTP)
    {
        UniqueID = uniqueId;
        ScenarioName = scenarioName;
        DefaultPlayerForEstateTP = defaultPlayerForEstateTP;
    }

    public Scenario(PlayerModel playerForEstateTp, string? scenarioName = null)
    {
        ScenarioName = scenarioName ?? playerForEstateTp.FullName;
        DefaultPlayerForEstateTP = playerForEstateTp.Clone();
    }
}
