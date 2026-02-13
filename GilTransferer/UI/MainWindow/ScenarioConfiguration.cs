using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Utility.Raii;
using GilTransferer.Helpers;
using NoireLib;
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
            if (ImGui.Button("Stop Processing Queue"))
            {
                Service.StopQueue();
            }

            ImGui.SameLine();

            if (Service.TaskQueue.IsQueueRunning())
            {
                if (ImGui.Button("Pause Queue"))
                    Service.TaskQueue.PauseQueue();
            }
            else
            {
                if (ImGui.Button("Resume Queue"))
                    Service.TaskQueue.ResumeQueue();
            }

            ImGui.SameLine();

            if (ImGui.Button("Skip Current Task"))
                Service.TaskQueue.SkipCurrentTask();

            ImGui.SameLine();

            if (ImGui.Button("Skip Current Character"))
                BuyingProcess.SkipCurrentCharacterPurchase();

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
            ImGui.Separator();
            ImGui.Spacing();

            var estateTPPlayerName = _selectedScenario!.DefaultPlayerForEstateTP.PlayerName ?? string.Empty;
            var estateTPHomeworld = _selectedScenario!.DefaultPlayerForEstateTP.Homeworld ?? string.Empty;

            ImGui.TextUnformatted("Default Player for Estate Teleport:");

            ImGui.SetNextItemWidth(200);
            if (ImGui.InputTextWithHint("##EstateTPName", "Player Name (No World)", ref estateTPPlayerName, 32))
            {
                _selectedScenario!.DefaultPlayerForEstateTP.PlayerName = estateTPPlayerName;

                // Reset CID since we manually changed the name,
                var arPlayer = Service.FindARPlayerFromPlayerModel(new PlayerModel(estateTPPlayerName, estateTPHomeworld));
                _selectedScenario!.DefaultPlayerForEstateTP.ContentId = arPlayer?.CID;
                _selectedScenario!.DefaultPlayerForEstateTP.WorldId = null;

                Configuration.Instance.Save();
            }

            ImGui.SameLine();
            ImGui.Text("@");
            ImGui.SameLine();

            ImGui.SetNextItemWidth(150);
            if (ImGui.InputTextWithHint("##EstateTPWorld", "World", ref estateTPHomeworld, 32))
            {
                _selectedScenario.DefaultPlayerForEstateTP.Homeworld = estateTPHomeworld;

                // Reset CID since we manually changed the name,
                var arPlayer = Service.FindARPlayerFromPlayerModel(new PlayerModel(estateTPPlayerName, estateTPHomeworld));
                _selectedScenario!.DefaultPlayerForEstateTP.ContentId = arPlayer?.CID;
                _selectedScenario!.DefaultPlayerForEstateTP.WorldId = null;

                Configuration.Instance.Save();
            }

            if (ImGui.Button("Set to Current Character##EstateTP"))
            {
                var localPlayer = NoireService.ObjectTable.LocalPlayer;
                if (localPlayer != null)
                {
                    _selectedScenario!.DefaultPlayerForEstateTP = new PlayerModel(localPlayer);
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
                    _selectedScenario!.DefaultPlayerForEstateTP = new PlayerModel((IPlayerCharacter)target!);
                    Configuration.Instance.Save();
                }
            }

            //ImGui.Spacing();

            //ImGui.TextUnformatted("Estate to teleport to:");

            //var currentDestination = (int)_selectedScenario!.DestinationType;
            //var destinationNames = Enum.GetNames(typeof(DestinationType));

            //ImGui.SetNextItemWidth(200);
            //if (ImGui.Combo("##DestinationType", ref currentDestination, destinationNames, destinationNames.Length))
            //{
            //    _selectedScenario.DestinationType = (DestinationType)currentDestination;
            //    Configuration.Instance.Save();
            //}

            //if (_selectedScenario!.DestinationType == DestinationType.FCChamber ||
            //    _selectedScenario!.DestinationType == DestinationType.Apartment)
            //{
            //    ImGui.Spacing();
            //    var chamberOrApartmentNumber = _selectedScenario.ChamberOrApartmentNumber;
            //    ImGui.TextUnformatted("Chamber/Apartment Number:");
            //    ImGui.SetNextItemWidth(200);
            //    if (ImGui.InputInt("##ChamberOrApartmentNumber", ref chamberOrApartmentNumber, flags: ImGuiInputTextFlags.CallbackCompletion))
            //    {
            //        chamberOrApartmentNumber = Math.Max(1, chamberOrApartmentNumber);
            //        _selectedScenario.ChamberOrApartmentNumber = chamberOrApartmentNumber;
            //        Configuration.Instance.Save();
            //    }
            //}

            //ImGui.Spacing();

            //var currentOutdoorDestinationTerritoryId = _selectedScenario.DestinationOutsideTerritoryId;
            //ImGui.TextUnformatted("Territory ID of Estate Outdoors: ");
            //ImGui.SameLine();
            //ImGui.TextUnformatted($"{(currentOutdoorDestinationTerritoryId == null ? "none" : currentOutdoorDestinationTerritoryId)}");
            //ImGui.Spacing();
            //if (ImGui.Button("Set to Current Territory ID##Outdoors"))
            //{
            //    var territoryId = NoireService.ClientState.TerritoryType;
            //    if (ExcelSheetHelper.GetSheet<TerritoryType>()?.TryGetRow(territoryId, out var territoryRow) ?? false)
            //    {
            //        _selectedScenario.DestinationOutsideTerritoryId = territoryRow.RowId;
            //        Configuration.Instance.Save();
            //    }
            //    else
            //    {
            //        NoireLogger.LogError($"Failed to get TerritoryType row for ID {territoryId}");
            //    }
            //}

            //var currentIndoorDestinationTerritoryId = _selectedScenario.DestinationIndoorTerritoryId;
            //ImGui.TextUnformatted("Territory ID of Estate Indoors: ");
            //ImGui.SameLine();
            //ImGui.TextUnformatted($"{(currentIndoorDestinationTerritoryId == null ? "none" : currentIndoorDestinationTerritoryId)}");
            //ImGui.Spacing();
            //if (ImGui.Button("Set to Current Territory ID##Indoors"))
            //{
            //    var territoryId = NoireService.ClientState.TerritoryType;
            //    if (ExcelSheetHelper.GetSheet<TerritoryType>()?.TryGetRow(territoryId, out var territoryRow) ?? false)
            //    {
            //        _selectedScenario.DestinationIndoorTerritoryId = territoryRow.RowId;
            //        Configuration.Instance.Save();
            //    }
            //    else
            //    {
            //        NoireLogger.LogError($"Failed to get TerritoryType row for ID {territoryId}");
            //    }
            //}

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Global Gils Transfer Settings:");
            ImGui.Spacing();

            var minGilsToConsiderCharacters = _selectedScenario!.MinGilsToConsiderCharacters;
            ImGui.TextUnformatted($"Ignore characters with less than X gil (Minimum {_selectedScenario!.GilsToLeaveOnCharacters}):");
            ImGui.SetNextItemWidth(200);
            if (ImGui.InputInt("##MinGilsToConsiderCharacters", ref minGilsToConsiderCharacters, flags: ImGuiInputTextFlags.CallbackCompletion))
            {
                minGilsToConsiderCharacters = Math.Max(_selectedScenario!.GilsToLeaveOnCharacters, minGilsToConsiderCharacters);
                _selectedScenario!.MinGilsToConsiderCharacters = minGilsToConsiderCharacters;
                Configuration.Instance.Save();
            }

            var gilsToLeaveOnCharacters = _selectedScenario!.GilsToLeaveOnCharacters;
            ImGui.TextUnformatted($"At the very least, this amount of gils will be left on each characters:");
            ImGui.SetNextItemWidth(200);
            if (ImGui.InputInt("##GilsToLeaveOnCharacters", ref gilsToLeaveOnCharacters))
            {
                gilsToLeaveOnCharacters = Math.Max(0, gilsToLeaveOnCharacters);
                _selectedScenario!.GilsToLeaveOnCharacters = gilsToLeaveOnCharacters;
                Configuration.Instance.Save();
            }
        }
    }
}
