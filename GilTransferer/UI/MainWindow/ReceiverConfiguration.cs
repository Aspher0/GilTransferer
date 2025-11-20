using Dalamud.Bindings.ImGui;
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
    private void DrawReceiverConfigurationMode()
    {
        if (_selectedReceiver == null)
        {
            _currentMode = UIMode.ReceiverSelection;
            return;
        }

        if (ImGui.Button("Back to List"))
        {
            _currentMode = UIMode.ReceiverSelection;
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
        ImGui.TextUnformatted($"Configuring: {_selectedReceiver.ReceivingPlayer?.FullName ?? "Unknown"}");

        ImGui.Separator();
        ImGui.Spacing();

        // Tab bar
        using (ImRaii.TabBar("##ReceiverConfigTabs"))
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

            ImGui.TextUnformatted("Receiving Player:");

            if (_selectedReceiver!.ReceivingPlayer != null)
            {
                var playerName = _selectedReceiver.ReceivingPlayer.PlayerName;
                var homeworld = _selectedReceiver.ReceivingPlayer.Homeworld;

                ImGui.SetNextItemWidth(200);
                if (ImGui.InputTextWithHint("##ReceiverName", "Player Name (No World)", ref playerName, 32))
                {
                    _selectedReceiver.ReceivingPlayer.PlayerName = playerName;
                    Configuration.Instance.Save();
                }

                ImGui.SameLine();
                ImGui.Text("@");
                ImGui.SameLine();

                ImGui.SetNextItemWidth(150);
                if (ImGui.InputTextWithHint("##ReceiverWorld", "World", ref homeworld, 32))
                {
                    _selectedReceiver.ReceivingPlayer.Homeworld = homeworld;
                    Configuration.Instance.Save();
                }

                if (ImGui.Button("Set to Current Character"))
                {
                    var localPlayer = NoireService.ClientState.LocalPlayer;
                    if (localPlayer != null)
                    {
                        _selectedReceiver.ReceivingPlayer = new PlayerModel(localPlayer);
                        Configuration.Instance.Save();
                    }
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            var estateTPPlayerName = _selectedReceiver!.PlayerForEstateTP.PlayerName ?? string.Empty;

            ImGui.TextUnformatted("Player for Estate Teleport:");

            ImGui.SetNextItemWidth(200);
            if (ImGui.InputTextWithHint("##EstateTPName", "Player Name (No World)", ref estateTPPlayerName, 32))
            {
                _selectedReceiver!.PlayerForEstateTP.PlayerName = estateTPPlayerName;
                Configuration.Instance.Save();
            }

            if (ImGui.Button("Set to Current Character##EstateTP"))
            {
                var localPlayer = NoireService.ClientState.LocalPlayer;
                if (localPlayer != null)
                {
                    _selectedReceiver!.PlayerForEstateTP = new PlayerModel(localPlayer);
                    Configuration.Instance.Save();
                }
            }

            ImGui.Spacing();

            ImGui.TextUnformatted("Estate to teleport to:");

            var currentDestination = (int)_selectedReceiver!.DestinationType;
            var destinationNames = Enum.GetNames(typeof(DestinationType));

            ImGui.SetNextItemWidth(200);
            if (ImGui.Combo("##DestinationType", ref currentDestination, destinationNames, destinationNames.Length))
            {
                _selectedReceiver.DestinationType = (DestinationType)currentDestination;
                Configuration.Instance.Save();
            }

            if (_selectedReceiver!.DestinationType == DestinationType.FCChamber ||
                _selectedReceiver!.DestinationType == DestinationType.Apartment)
            {
                ImGui.Spacing();
                var chamberOrApartmentNumber = _selectedReceiver.ChamberOrApartmentNumber;
                ImGui.TextUnformatted("Chamber/Apartment Number:");
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputInt("##ChamberOrApartmentNumber", ref chamberOrApartmentNumber, flags: ImGuiInputTextFlags.CallbackCompletion))
                {
                    chamberOrApartmentNumber = Math.Max(1, chamberOrApartmentNumber);
                    _selectedReceiver.ChamberOrApartmentNumber = chamberOrApartmentNumber;
                    Configuration.Instance.Save();
                }
            }

            ImGui.Spacing();

            var currentOutdoorDestinationTerritoryId = _selectedReceiver.DestinationOutsideTerritoryId;
            ImGui.TextUnformatted("Territory ID of Estate Outdoors: ");
            ImGui.SameLine();
            ImGui.TextUnformatted($"{(currentOutdoorDestinationTerritoryId == null ? "none" : currentOutdoorDestinationTerritoryId)}");
            ImGui.Spacing();
            if (ImGui.Button("Set to Current Territory ID##Outdoors"))
            {
                var territoryId = NoireService.ClientState.TerritoryType;
                if (ExcelSheetHelper.GetSheet<TerritoryType>()?.TryGetRow(territoryId, out var territoryRow) ?? false)
                {
                    _selectedReceiver.DestinationOutsideTerritoryId = territoryRow.RowId;
                    Configuration.Instance.Save();
                }
                else
                {
                    NoireLogger.LogError($"Failed to get TerritoryType row for ID {territoryId}");
                }
            }

            var currentIndoorDestinationTerritoryId = _selectedReceiver.DestinationIndoorTerritoryId;
            ImGui.TextUnformatted("Territory ID of Estate Indoors: ");
            ImGui.SameLine();
            ImGui.TextUnformatted($"{(currentIndoorDestinationTerritoryId == null ? "none" : currentIndoorDestinationTerritoryId)}");
            ImGui.Spacing();
            if (ImGui.Button("Set to Current Territory ID##Indoors"))
            {
                var territoryId = NoireService.ClientState.TerritoryType;
                if (ExcelSheetHelper.GetSheet<TerritoryType>()?.TryGetRow(territoryId, out var territoryRow) ?? false)
                {
                    _selectedReceiver.DestinationIndoorTerritoryId = territoryRow.RowId;
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
