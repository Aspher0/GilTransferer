using GilTransferer.Enums;
using GilTransferer.Models;
using NoireLib.Configuration;
using System;
using System.Collections.Generic;

namespace GilTransferer;

[Serializable]
public class Configuration : NoireConfigBase<Configuration>
{
    public override int Version { get; set; } = 1;

    public override string GetConfigFileName() => "GilTransfererConfig";

    /// <summary>
    /// A list of scenarios configured by the user.
    /// </summary>
    [AutoSave]
    public virtual List<Scenario> Scenarios { get; set; } = new();

    /// <summary>
    /// The mapping of slot types to the item assigned to it.
    /// </summary>
    [AutoSave]
    public virtual Dictionary<SlotType, uint> ItemsPerSlot { get; set; } = new();

    /// <summary>
    /// Determines the minimum amount of gils a character must have to be considered for gil transfer to this scenario.
    /// </summary>
    [AutoSave]
    public virtual int MinGilsToConsiderCharacters { get; set; } = 100000;

    /// <summary>
    /// Determines the amount of gils to keep on each characters.
    /// </summary>
    // Todo: Make editable in the future (if so add virtual modifier for NoireConfigBase auto save)
    public readonly int GilsToLeaveOnCharacters = 25000;
}
