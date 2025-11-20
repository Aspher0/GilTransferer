using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using GilTransferer.Models;
using NoireLib;
using NoireLib.Models;
using System.Numerics;

namespace GilTransferer.UI;

public partial class MainWindow
{
    private void DrawScenarioSelectionMode()
    {
        ImGui.TextUnformatted("Select or Create a Scenario");
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("Add Current Character"))
        {
            var localPlayer = NoireService.ClientState.LocalPlayer;
            if (localPlayer != null)
            {
                var newScenario = new Scenario(new PlayerModel(localPlayer));
                Configuration.Instance.Scenarios.Add(newScenario);
                Configuration.Instance.Save();
            }
        }

        ImGui.SameLine();

        if (ImGui.Button("Add Manually"))
        {
            var newScenario = new Scenario(new PlayerModel("New Character", "World"));
            Configuration.Instance.Scenarios.Add(newScenario);
            Configuration.Instance.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("Available Scenarios:");
        ImGui.Spacing();

        var availableYSpace = ImGui.GetContentRegionAvail().Y;
        float buttonAreaHeight = 25.0f;

        using (ImRaii.Child("##ScenarioListChild", new Vector2(-1, availableYSpace - buttonAreaHeight - 10), true))
        {
            var scenarios = Configuration.Instance.Scenarios;

            if (scenarios.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "No scenarios configured yet. Add one to get started!");
            }
            else
            {
                for (int i = 0; i < scenarios.Count; i++)
                {
                    var scenario = scenarios[i];
                    bool isSelected = _selectedScenarioIndex == i;

                    using (var id = ImRaii.PushId(i))
                    {
                        if (ImGui.Selectable($"##Scenario{i}", isSelected, ImGuiSelectableFlags.None, new Vector2(0, 40)))
                        {
                            _selectedScenarioIndex = i;
                            _selectedScenario = scenario;
                        }

                        // Right-click context menu
                        var ioScenario = ImGui.GetIO();
                        bool ctrlShiftHeldScenario = ioScenario.KeyCtrl && ioScenario.KeyShift;

                        if (ImGui.BeginPopupContextItem($"##ScenarioContext{i}"))
                        {
                            using (ImRaii.Disabled(!ctrlShiftHeldScenario))
                            {
                                if (ImGui.MenuItem("Delete"))
                                {
                                    scenarios.RemoveAt(i);
                                    Configuration.Instance.Save();
                                    if (_selectedScenarioIndex == i)
                                    {
                                        _selectedScenarioIndex = -1;
                                        _selectedScenario = null;
                                    }
                                    ImGui.EndPopup();
                                    return;
                                }
                            }

                            if (!ctrlShiftHeldScenario && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                ImGui.SetTooltip("Hold CTRL and Shift to delete");
                            }

                            ImGui.EndPopup();
                        }

                        if (ImGui.IsItemVisible())
                        {
                            var cursorPos = ImGui.GetCursorPos();
                            ImGui.SetCursorPos(new Vector2(cursorPos.X, cursorPos.Y - 42.5f));

                            ImGui.TextUnformatted($"{scenario.ScenarioName ?? "Unknown Scenario"}");
                            ImGui.TextColored(new Vector4(0.6f, 0.8f, 1.0f, 1.0f),
                              $"{scenario.DestinationType} | {scenario.Mannequins.Count} Mannequin(s)");

                            ImGui.SetCursorPos(cursorPos);
                        }
                    }

                    ImGui.Spacing();
                }
            }
        }

        ImGui.Spacing();

        var availableXSpace = ImGui.GetContentRegionAvail().X;
        var buttonWidth = availableXSpace / 2 - 10;

        using (ImRaii.Disabled(_selectedScenario == null))
        {
            if (ImGui.Button("Select This Scenario", new Vector2(buttonWidth, 25)))
            {
                if (_selectedScenario != null)
                {
                    _currentMode = UIMode.ScenarioConfiguration;
                    _selectedMannequin = null;
                    _selectedMannequinIndex = -1;
                }
            }
        }

        ImGui.SameLine();

        var io = ImGui.GetIO();
        bool ctrlShiftHeld2 = io.KeyCtrl && io.KeyShift;

        using (ImRaii.Disabled(_selectedScenario == null || !ctrlShiftHeld2))
        {
            if (ImGui.Button("Delete", new Vector2(buttonWidth, 25)))
            {
                if (_selectedScenario != null)
                {
                    Configuration.Instance.Scenarios.RemoveAt(_selectedScenarioIndex);
                    Configuration.Instance.Save();
                    _selectedScenario = null;
                    _selectedScenarioIndex = -1;
                }
            }
        }

        if (_selectedScenario != null && !ctrlShiftHeld2 && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Hold CTRL and Shift to delete");
    }
}
