using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using GilTransferer.Enums;
using GilTransferer.Helpers;
using GilTransferer.Models;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace GilTransferer.UI;

public partial class MainWindow
{
    private static Dictionary<string, (ulong cid, string name, string world, long gil, string displayText)> characterLookup = [];

    private unsafe void DrawMannequinsTab()
    {
        float availableYSpace = ImGui.GetContentRegionAvail().Y;
        float buttonAreaHeight = 25.0f;

        using (ImRaii.Child("##SettingsWindow", new Vector2(-1, availableYSpace - buttonAreaHeight - 10)))
        {
            // Left panel - Mannequin list
            availableYSpace = ImGui.GetContentRegionAvail().Y;
            using (ImRaii.Child("##MannequinListPanel", new Vector2(250, availableYSpace), true))
            {
                ImGui.TextUnformatted("Mannequin List:");
                ImGui.Separator();
                ImGui.Spacing();

                for (int i = 0; i < _selectedScenario!.Mannequins.Count; i++)
                {
                    var mannequin = _selectedScenario.Mannequins[i];
                    bool isSelected = _selectedMannequinIndex == i;

                    using (ImRaii.PushId(i))
                    {
                        var isTargeted = NoireService.TargetManager.Target is INpc targetNpc2 &&
                                         mannequin.Equals(CommonHelper.MakeMannequin(targetNpc2), true);

                        if (ImGui.Selectable($"Mannequin #{i + 1}{(isTargeted ? " (Current Target)" : "")}##Mannequin{i}", isSelected))
                        {
                            _selectedMannequinIndex = i;
                            _selectedMannequin = mannequin;
                        }

                        var io = ImGui.GetIO();
                        bool ctrlShiftHeld = io.KeyCtrl && io.KeyShift;

                        using (var contex = ImRaii.ContextPopupItem($"##MannequinContext{i}"))
                        {
                            if (contex)
                            {
                                using (ImRaii.Disabled(!ctrlShiftHeld))
                                {
                                    if (ImGui.MenuItem("Delete"))
                                    {
                                        _selectedScenario.Mannequins.RemoveAt(i);
                                        Configuration.Instance.Save();
                                        if (_selectedMannequinIndex == i)
                                        {
                                            _selectedMannequinIndex = -1;
                                            _selectedMannequin = null;
                                        }
                                        return;
                                    }
                                }
                                if (!ctrlShiftHeld && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                                {
                                    ImGui.SetTooltip("Hold CTRL and Shift to delete");
                                }
                            }
                        }
                    }
                }
            }

            ImGui.SameLine();

            availableYSpace = ImGui.GetContentRegionAvail().Y;
            using (ImRaii.Child("##MannequinDetailsPanel", new Vector2(-1, availableYSpace), true))
            {
                if (_selectedMannequin == null)
                {
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Select a mannequin to view details");
                }
                else
                {
                    ImGui.TextUnformatted("Mannequin Details:");
                    ImGui.Separator();
                    ImGui.Spacing();

                    var baseId = _selectedMannequin.BaseId;
                    var companionOwnerId = _selectedMannequin.CompanionOwnerId;

                    ImGui.Text($"Base ID: {baseId}");
                    ImGui.Text($"Companion Owner ID: {companionOwnerId}");

                    var placeName = ExcelSheetHelper.GetSheet<PlaceName>()!.GetRow(_selectedMannequin.PlaceNameId)!.Name.ExtractText();

                    var playerForTp = _selectedMannequin.PlayerForEstateTPOverride ?? _selectedScenario.DefaultPlayerForEstateTP;
                    ImGui.Text($"[{playerForTp.Homeworld}] {placeName} W{_selectedMannequin.Ward} P{_selectedMannequin.Plot}{(_selectedMannequin.ChamberOrApartmentNumber == 0 ? "" : $" Room {_selectedMannequin.ChamberOrApartmentNumber}")} ({_selectedMannequin.DestinationType})");

                    ImGui.SameLine();

                    if (NoireService.TargetManager.Target is INpc targetNpc3 && targetNpc3.ObjectKind == ObjectKind.EventNpc)
                    {
                        var npcNative = CharacterHelper.GetCharacterAddress(targetNpc3);
                        var targetBaseId = targetNpc3.BaseId;
                        var targetCompanionOwnerId = npcNative->CompanionOwnerId;

                        var existingIndex = GetTargetMannequinIndex(targetNpc3);

                        if (existingIndex >= 0)
                        {
                            using (ImRaii.Disabled(true))
                                ImGui.Button("Update with target");

                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                                ImGui.SetTooltip("Target already configured");
                        }
                        else
                        {
                            if (ImGui.Button($"Update with target"))
                            {
                                var updatedMannequin = CommonHelper.MakeMannequin(targetNpc3);
                                _selectedScenario!.Mannequins[_selectedMannequinIndex] = updatedMannequin;
                                _selectedMannequin = updatedMannequin;
                                Configuration.Instance.Save();
                            }
                        }
                    }
                    else
                    {
                        using (ImRaii.Disabled(true))
                            ImGui.Button("Update with target");

                        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            ImGui.SetTooltip("Target isn't a mannequin");
                    }

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    if (ImGui.Button("Setup this Mannequin"))
                    {
                        SellingProcess.ProcessMannequin(_selectedMannequin, _selectedScenario, true);
                    }

                    availableYSpace = ImGui.GetContentRegionAvail().Y;
                    using (ImRaii.Child("##SlotsChild", new Vector2(-1, availableYSpace), false))
                    {
                        ImGui.TextUnformatted("Mannequin Slots:");
                        ImGui.Spacing();

                        var showAllChars = Configuration.Instance.ShowAllCharsInComboBox;
                        if (ImGui.Checkbox("Show All Characters (Regardless of min. amount of gils)", ref showAllChars))
                            Configuration.Instance.ShowAllCharsInComboBox = showAllChars;

                        ImGui.Spacing();

                        var hideAssignedChars = Configuration.Instance.HideAlreadyAssignedCharactersInComboBox;
                        if (ImGui.Checkbox("Hide Already Assigned Characters", ref hideAssignedChars))
                            Configuration.Instance.HideAlreadyAssignedCharactersInComboBox = hideAssignedChars;

                        BuildCharacterList();

                        foreach (SlotType slotType in Enum.GetValues<SlotType>())
                        {
                            if (!_selectedMannequin.Slots.TryGetValue(slotType, out var slot))
                            {
                                slot = new MannequinSlot(slotType);
                                _selectedMannequin.Slots[slotType] = slot;
                            }

                            using (ImRaii.PushId((int)slotType))
                            {
                                ImGui.AlignTextToFramePadding();
                                var cursorPos = ImGui.GetCursorPos();
                                ImGui.SetNextItemWidth(75);
                                ImGui.Text(slotType.ToString());
                                ImGui.SameLine();
                                ImGui.SetCursorPosX(cursorPos.X + 75);

                                string currentSelectionText = "None";
                                if (slot.AssignedCharacter != null && Service.AutoRetainerAPI.Ready)
                                {
                                    var key = GetCharacterKey(slot.AssignedCharacter.PlayerName, slot.AssignedCharacter.Homeworld);
                                    if (characterLookup.TryGetValue(key, out var match))
                                        currentSelectionText = match.displayText;
                                    else
                                        currentSelectionText = $"{slot.AssignedCharacter.PlayerName}@{slot.AssignedCharacter.Homeworld}";
                                }

                                if (!_slotSearchFilters.ContainsKey(slotType))
                                    _slotSearchFilters[slotType] = string.Empty;

                                ImGui.SetNextItemWidth(300);

                                if (ImGui.BeginCombo($"##Character{slotType}", currentSelectionText, ImGuiComboFlags.HeightLarge))
                                {
                                    var searchFilter = _slotSearchFilters[slotType];
                                    ImGui.SetNextItemWidth(-1);
                                    if (ImGui.InputTextWithHint("##Search", "Search...", ref searchFilter, 256))
                                    {
                                        _slotSearchFilters[slotType] = searchFilter;
                                    }

                                    ImGui.Separator();

                                    if (string.IsNullOrEmpty(searchFilter) || "none".Contains(searchFilter.ToLower()))
                                    {
                                        bool isNoneSelected = slot.AssignedCharacter == null;
                                        if (ImGui.Selectable("None", isNoneSelected))
                                        {
                                            slot.AssignedCharacter = null;
                                            Configuration.Instance.Save();
                                            _slotSearchFilters[slotType] = string.Empty;
                                            ImGui.CloseCurrentPopup();
                                        }
                                    }

                                    foreach (var (key, charInfo) in characterLookup)
                                    {
                                        if (
                                            !showAllChars &&
                                            (charInfo.gil - _selectedScenario!.GilsToLeaveOnCharacters <= 0 ||
                                            charInfo.gil < _selectedScenario!.MinGilsToConsiderCharacters
                                            ))
                                        {
                                            continue;
                                        }

                                        if (!string.IsNullOrEmpty(searchFilter) &&
                                           !charInfo.displayText.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
                                        {
                                            continue;
                                        }

                                        bool isSelected = slot.AssignedCharacter != null &&
                                        slot.AssignedCharacter.PlayerName == charInfo.name &&
                                        slot.AssignedCharacter.Homeworld == charInfo.world;

                                        bool isAssignedElsewhere = CommonHelper.IsCharacterAssignedAnywhere(_selectedScenario, charInfo.name, charInfo.world, out string assignmentInfo);

                                        if (hideAssignedChars && isAssignedElsewhere && !isSelected)
                                            continue;

                                        // Color code: Orange/Yellow for already assigned characters
                                        if (isAssignedElsewhere)
                                        {
                                            // Need to use ImRaii here to ensure proper pop
                                            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.7f, 0.2f, 1.0f));
                                        }

                                        if (ImGui.Selectable(charInfo.displayText, isSelected))
                                        {
                                            slot.AssignedCharacter = new PlayerModel(charInfo.name, charInfo.world, contentId: charInfo.cid);
                                            long gilsAvailable = charInfo.gil - _selectedScenario!.GilsToLeaveOnCharacters;
                                            Configuration.Instance.Save();
                                            _slotSearchFilters[slotType] = string.Empty;
                                            ImGui.CloseCurrentPopup();
                                        }

                                        if (isAssignedElsewhere)
                                            ImGui.PopStyleColor();

                                        if (isAssignedElsewhere && ImGui.IsItemHovered())
                                            ImGui.SetTooltip($"Already assigned to: {assignmentInfo}");
                                    }

                                    ImGui.EndCombo();
                                }

                                // Right-click to clear the combo box
                                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                                {
                                    slot.AssignedCharacter = null;
                                    Configuration.Instance.Save();
                                }

                                if (ImGui.IsItemHovered())
                                    ImGui.SetTooltip($"Right click to clear");

                                ImGui.SameLine();

                                if (slot.AssignedCharacter != null && Service.AutoRetainerAPI.Ready)
                                {
                                    var key = GetCharacterKey(slot.AssignedCharacter.PlayerName, slot.AssignedCharacter.Homeworld);
                                    if (characterLookup.TryGetValue(key, out var charInfo))
                                    {
                                        bool willBeIgnored = charInfo.gil < _selectedScenario!.MinGilsToConsiderCharacters || (charInfo.gil - _selectedScenario!.GilsToLeaveOnCharacters <= 0);

                                        using (ImRaii.PushFont(UiBuilder.IconFont))
                                        {
                                            if (willBeIgnored)
                                                ImGui.TextColored(new Vector4(1.0f, 0.3f, 0.3f, 1.0f), FontAwesomeIcon.Times.ToIconString());
                                            else
                                                ImGui.TextColored(new Vector4(0.3f, 1.0f, 0.3f, 1.0f), FontAwesomeIcon.Check.ToIconString());
                                        }

                                        if (willBeIgnored && ImGui.IsItemHovered())
                                            ImGui.SetTooltip($"Character will be ignored (has {charInfo.gil:N0} gil, minimum is {_selectedScenario!.MinGilsToConsiderCharacters:N0})");
                                        else if (!willBeIgnored && ImGui.IsItemHovered())
                                            ImGui.SetTooltip($"Character will be processed ({(charInfo.gil - _selectedScenario!.GilsToLeaveOnCharacters):N0} gil to transfer)");
                                    }
                                }
                            }

                            ImGui.Spacing();
                        }
                    }
                }
            }
        }

        ImGui.Spacing();

        var target = NoireService.TargetManager.Target;
        var availableXSpace = ImGui.GetContentRegionAvail().X;
        var buttonWidth = availableXSpace / 4 - 10;

        if (target is INpc targetNpc && targetNpc.ObjectKind == ObjectKind.EventNpc)
        {
            var npcNative = CharacterHelper.GetCharacterAddress(targetNpc);
            var targetBaseId = targetNpc.BaseId;
            var targetCompanionOwnerId = npcNative->CompanionOwnerId;

            var existingIndex = GetTargetMannequinIndex(targetNpc);

            if (existingIndex >= 0)
            {
                if (ImGui.Button($"Edit {targetNpc!.Name}", new Vector2(buttonWidth, 25)))
                {
                    var existingMannequin = _selectedScenario!.Mannequins[existingIndex];
                    _selectedMannequinIndex = existingIndex;
                    _selectedMannequin = existingMannequin;
                }
            }
            else
            {
                if (ImGui.Button($"Add {targetNpc!.Name}", new Vector2(buttonWidth, 25)))
                {
                    var newMannequin = CommonHelper.MakeMannequin(targetNpc);
                    _selectedScenario!.Mannequins.Add(newMannequin);
                    Configuration.Instance.Save();

                    _selectedMannequinIndex = _selectedScenario.Mannequins.Count - 1;
                    _selectedMannequin = newMannequin;
                }
            }
        }
        else
        {
            using (ImRaii.Disabled(true))
                ImGui.Button("Target isn't Mannequin", new Vector2(buttonWidth, 25));
        }

        ImGui.SameLine();

        var io2 = ImGui.GetIO();
        bool ctrlShiftHeld2 = io2.KeyCtrl && io2.KeyShift;

        using (ImRaii.Disabled(_selectedMannequin == null || !ctrlShiftHeld2))
        {
            if (ImGui.Button("Remove Selected", new Vector2(buttonWidth, 25)))
            {
                if (_selectedMannequin != null && _selectedMannequinIndex >= 0)
                {
                    _selectedScenario!.Mannequins.RemoveAt(_selectedMannequinIndex);
                    Configuration.Instance.Save();
                    _selectedMannequinIndex = -1;
                    _selectedMannequin = null;
                }
            }
        }
        if (_selectedMannequin != null && !ctrlShiftHeld2 && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Hold CTRL and Shift to delete");

        ImGui.SameLine();

        if (ImGui.Button("Setup All Mannequins", new Vector2(buttonWidth, 25)))
            SellingProcess.SetupAllMannequins(_selectedScenario);

        ImGui.SameLine();

        if (ImGui.Button("Process Characters Buying", new Vector2(buttonWidth, 25)))
            BuyingProcess.ProcessAllCharacterPurchases(_selectedScenario);
    }

    private static void BuildCharacterList()
    {
        ThrottleHelper.Throttle("BuildCharacterList", () =>
        {
            if (!Service.AutoRetainerAPI.Ready)
                return;

            characterLookup.Clear();

            var characterList = new List<(ulong cid, string name, string world, long gil, string displayText)>();
            foreach (var cid in Service.RegisteredCharacters)
            {
                var data = Service.GetOfflineCharacterData(cid);
                if (data == null)
                    continue;

                characterList.Add((cid, data.Name, data.World, data.Gil, $"{data.Name}@{data.World} ({data.Gil:N0} gil)"));
            }

            characterLookup = characterList.ToDictionary(x => GetCharacterKey(x.name, x.world));
        }, 5000);
    }

    private static string GetCharacterKey(string name, string world) => $"{name}@{world}";

    private int GetTargetMannequinIndex(INpc targetNpc)
    {
        if (_selectedScenario == null)
            return -1;

        var targetMannequin = CommonHelper.MakeMannequin(targetNpc);
        for (int i = 0; i < _selectedScenario.Mannequins.Count; i++)
        {
            if (_selectedScenario.Mannequins[i].Equals(targetMannequin, true))
                return i;
        }

        return -1;
    }
}
