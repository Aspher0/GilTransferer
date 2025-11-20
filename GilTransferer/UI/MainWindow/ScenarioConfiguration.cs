using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Utility.Raii;
using GilTransferer.Enums;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using NoireLib.Models;
using System;
using System.Numerics;

namespace GilTransferer.UI;

public partial class MainWindow
{
    private void DrawScenarioConfigurationMode()
    {
        if (_selectedScenario == null)
        {
            _currentMode = UIMode.ScenarioSelection;
            return;
        }

        if (ImGui.Button("Back to List"))
        {
            _currentMode = UIMode.ScenarioSelection;
            Configuration.Instance.Save();
            return;
        }

        if (Service.TaskQueue.IsQueueProcessing())
        {
            ImGui.SameLine();

            if (ImGui.Button("Stop Processing Queue"))
            {
                Service.StopQueue();
                return;
            }

            ImGui.SameLine();
            ImGui.TextUnformatted($"(Processing {Math.Floor(Service.TaskQueue.GetQueueProgress() * 100)}%)");
        }

        ImGui.SameLine();
        ImGui.TextUnformatted($"Configuring: {_selectedScenario.ScenarioName ?? "Unknown Scenario"}");

        ImGui.Separator();
        ImGui.Spacing();

        // Tab bar
        using (ImRaii.TabBar("##ScenarioConfigTabs"))
        {
            using (var configTab = ImRaii.TabItem("Config"))
            {
                if (configTab)
                {
                    DrawConfigTab();
                }
            }

            using (var configSlotsTab = ImRaii.TabItem("Global Slot Items Config"))
            {
                if (configSlotsTab)
                {
                    DrawConfigSlotsTab();
                }
            }

            using (var mannequinsTab = ImRaii.TabItem("Mannequins"))
            {
                if (mannequinsTab)
                {
                    DrawMannequinsTab();
                }
            }
        }
    }

    private void DrawConfigTab()
    {
        using (ImRaii.Child("##ConfigChild", new Vector2(-1, -1), false))
        {
            ImGui.Spacing();

            ImGui.TextUnformatted("Scenario Name:");

            var scenarioName = _selectedScenario!.ScenarioName;

            if (ImGui.InputTextWithHint("##ScenarioName", "My Scenario", ref scenarioName, 32))
            {
                _selectedScenario.ScenarioName = scenarioName;
                Configuration.Instance.Save();
            }

            ImGui.Spacing();

            ImGui.TextUnformatted("Receiving Player:");

            if (_selectedScenario!.ReceivingPlayer != null)
            {
                var playerName = _selectedScenario.ReceivingPlayer.PlayerName;
                var homeworld = _selectedScenario.ReceivingPlayer.Homeworld;

                ImGui.SetNextItemWidth(200);
                if (ImGui.InputTextWithHint("##ReceiverName", "Player Name (No World)", ref playerName, 32))
                {
                    _selectedScenario.ReceivingPlayer.PlayerName = playerName;
                    Configuration.Instance.Save();
                }

                ImGui.SameLine();
                ImGui.Text("@");
                ImGui.SameLine();

                ImGui.SetNextItemWidth(150);
                if (ImGui.InputTextWithHint("##ReceiverWorld", "World", ref homeworld, 32))
                {
                    _selectedScenario.ReceivingPlayer.Homeworld = homeworld;
                    Configuration.Instance.Save();
                }

                if (ImGui.Button("Set to Current Character"))
                {
                    var localPlayer = NoireService.ClientState.LocalPlayer;
                    if (localPlayer != null)
                    {
                        _selectedScenario.ReceivingPlayer = new PlayerModel(localPlayer);
                        Configuration.Instance.Save();
                    }
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            var estateTPPlayerName = _selectedScenario!.PlayerForEstateTP.PlayerName ?? string.Empty;
            var estateTPHomeworld = _selectedScenario!.PlayerForEstateTP.Homeworld ?? string.Empty;

            ImGui.TextUnformatted("Player for Estate Teleport:");

            ImGui.SetNextItemWidth(200);
            if (ImGui.InputTextWithHint("##EstateTPName", "Player Name (No World)", ref estateTPPlayerName, 32))
            {
                _selectedScenario!.PlayerForEstateTP.PlayerName = estateTPPlayerName;
                // Reset CID since we manually changed the name,
                // it's not used anyway but if we happen to use it in the future we will see there is an issue
                _selectedScenario!.PlayerForEstateTP.ContentId = null;
                _selectedScenario!.PlayerForEstateTP.WorldId = null;
                Configuration.Instance.Save();
            }

            ImGui.SameLine();
            ImGui.Text("@");
            ImGui.SameLine();

            ImGui.SetNextItemWidth(150);
            if (ImGui.InputTextWithHint("##EstateTPWorld", "World", ref estateTPHomeworld, 32))
            {
                _selectedScenario.PlayerForEstateTP.Homeworld = estateTPHomeworld;
                // Reset CID since we manually changed the name,
                // it's not used anyway but if we happen to use it in the future we will see there is an issue
                _selectedScenario!.PlayerForEstateTP.ContentId = null;
                _selectedScenario!.PlayerForEstateTP.WorldId = null;
                Configuration.Instance.Save();
            }

            if (ImGui.Button("Set to Current Character##EstateTP"))
            {
                var localPlayer = NoireService.ClientState.LocalPlayer;
                if (localPlayer != null)
                {
                    _selectedScenario!.PlayerForEstateTP = new PlayerModel(localPlayer);
                    Configuration.Instance.Save();
                }
            }

            ImGui.SameLine();

            var target = NoireService.TargetManager.Target;
            string? targetPlayerName = null;
            string? targetPlayerWorld = null;
            bool isPlayer = target is IPlayerCharacter;
            if (target is IPlayerCharacter playerChar)
            {
                targetPlayerName = playerChar.Name.TextValue;
                targetPlayerWorld = playerChar.HomeWorld.Value.Name.ExtractText();
            }

            using (ImRaii.Disabled(!isPlayer))
            {
                if (ImGui.Button($"Set to Current Target{(isPlayer ? $" ({targetPlayerName}@{targetPlayerWorld})" : " (Invalid Player)")}##EstateTP"))
                {
                    _selectedScenario!.PlayerForEstateTP = new PlayerModel((IPlayerCharacter)target!);
                    Configuration.Instance.Save();
                }
            }

            ImGui.Spacing();

            ImGui.TextUnformatted("Estate to teleport to:");

            var currentDestination = (int)_selectedScenario!.DestinationType;
            var destinationNames = Enum.GetNames(typeof(DestinationType));

            ImGui.SetNextItemWidth(200);
            if (ImGui.Combo("##DestinationType", ref currentDestination, destinationNames, destinationNames.Length))
            {
                _selectedScenario.DestinationType = (DestinationType)currentDestination;
                Configuration.Instance.Save();
            }

            if (_selectedScenario!.DestinationType == DestinationType.FCChamber ||
                _selectedScenario!.DestinationType == DestinationType.Apartment)
            {
                ImGui.Spacing();
                var chamberOrApartmentNumber = _selectedScenario.ChamberOrApartmentNumber;
                ImGui.TextUnformatted("Chamber/Apartment Number:");
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputInt("##ChamberOrApartmentNumber", ref chamberOrApartmentNumber, flags: ImGuiInputTextFlags.CallbackCompletion))
                {
                    chamberOrApartmentNumber = Math.Max(1, chamberOrApartmentNumber);
                    _selectedScenario.ChamberOrApartmentNumber = chamberOrApartmentNumber;
                    Configuration.Instance.Save();
                }
            }

            ImGui.Spacing();

            var currentOutdoorDestinationTerritoryId = _selectedScenario.DestinationOutsideTerritoryId;
            ImGui.TextUnformatted("Territory ID of Estate Outdoors: ");
            ImGui.SameLine();
            ImGui.TextUnformatted($"{(currentOutdoorDestinationTerritoryId == null ? "none" : currentOutdoorDestinationTerritoryId)}");
            ImGui.Spacing();
            if (ImGui.Button("Set to Current Territory ID##Outdoors"))
            {
                var territoryId = NoireService.ClientState.TerritoryType;
                if (ExcelSheetHelper.GetSheet<TerritoryType>()?.TryGetRow(territoryId, out var territoryRow) ?? false)
                {
                    _selectedScenario.DestinationOutsideTerritoryId = territoryRow.RowId;
                    Configuration.Instance.Save();
                }
                else
                {
                    NoireLogger.LogError($"Failed to get TerritoryType row for ID {territoryId}");
                }
            }

            var currentIndoorDestinationTerritoryId = _selectedScenario.DestinationIndoorTerritoryId;
            ImGui.TextUnformatted("Territory ID of Estate Indoors: ");
            ImGui.SameLine();
            ImGui.TextUnformatted($"{(currentIndoorDestinationTerritoryId == null ? "none" : currentIndoorDestinationTerritoryId)}");
            ImGui.Spacing();
            if (ImGui.Button("Set to Current Territory ID##Indoors"))
            {
                var territoryId = NoireService.ClientState.TerritoryType;
                if (ExcelSheetHelper.GetSheet<TerritoryType>()?.TryGetRow(territoryId, out var territoryRow) ?? false)
                {
                    _selectedScenario.DestinationIndoorTerritoryId = territoryRow.RowId;
                    Configuration.Instance.Save();
                }
                else
                {
                    NoireLogger.LogError($"Failed to get TerritoryType row for ID {territoryId}");
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Global Gils Transfer Settings:");
            ImGui.Spacing();

            var minGilsToConsiderCharacters = Configuration.Instance.MinGilsToConsiderCharacters;
            ImGui.TextUnformatted($"Ignore characters with less than X gil (Minimum {Configuration.Instance.GilsToLeaveOnCharacters}):");
            ImGui.SetNextItemWidth(200);
            if (ImGui.InputInt("##MinGilsToConsiderCharacters", ref minGilsToConsiderCharacters, flags: ImGuiInputTextFlags.CallbackCompletion))
            {
                minGilsToConsiderCharacters = Math.Max(Configuration.Instance.GilsToLeaveOnCharacters, minGilsToConsiderCharacters);
                Configuration.Instance.MinGilsToConsiderCharacters = minGilsToConsiderCharacters;
                Configuration.Instance.Save();
            }

            var gilsToLeaveOnCharacters = Configuration.Instance.GilsToLeaveOnCharacters;
            ImGui.TextUnformatted($"At the very least, this amount of gils will be left on each characters:");
            using (ImRaii.Disabled(true))
            {
                ImGui.SetNextItemWidth(200);
                ImGui.InputInt("##GilsToLeaveOnCharacters", ref gilsToLeaveOnCharacters);
            }
        }
    }
}
