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
    /// A flag indicating whether to show all characters in the combo box, regardless of their gil amount.
    /// </summary>
    [AutoSave]
    public virtual bool ShowAllCharsInComboBox { get; set; } = false;

    /// <summary>
    /// A flag indicating whether to hide characters that are already assigned to any mannequin slot in the combo box.
    /// </summary>
    [AutoSave]
    public virtual bool HideAlreadyAssignedCharactersInComboBox { get; set; } = false;
}
