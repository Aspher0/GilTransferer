using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using GilTransferer.Models;
using NoireLib;
using NoireLib.Models;
using System.Numerics;

namespace GilTransferer.UI;

public partial class MainWindow
{
    private void DrawReceiverSelectionMode()
    {
        ImGui.TextUnformatted("Select or Create a Receiver");
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("Add Current Character"))
        {
            var localPlayer = NoireService.ClientState.LocalPlayer;
            if (localPlayer != null)
            {
                var newReceiver = new Receiver(new PlayerModel(localPlayer));
                Configuration.Instance.Receivers.Add(newReceiver);
                Configuration.Instance.Save();
            }
        }

        ImGui.SameLine();

        if (ImGui.Button("Add Manually"))
        {
            var newReceiver = new Receiver(new PlayerModel("New Character", "World"));
            Configuration.Instance.Receivers.Add(newReceiver);
            Configuration.Instance.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("Available Receivers:");
        ImGui.Spacing();

        var availableYSpace = ImGui.GetContentRegionAvail().Y;
        float buttonAreaHeight = 25.0f;

        using (ImRaii.Child("##ReceiverListChild", new Vector2(-1, availableYSpace - buttonAreaHeight - 10), true))
        {
            var receivers = Configuration.Instance.Receivers;

            if (receivers.Count == 0)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "No receivers configured yet. Add one to get started!");
            }
            else
            {
                for (int i = 0; i < receivers.Count; i++)
                {
                    var receiver = receivers[i];
                    bool isSelected = _selectedReceiverIndex == i;

                    using (var id = ImRaii.PushId(i))
                    {
                        if (ImGui.Selectable($"##Receiver{i}", isSelected, ImGuiSelectableFlags.None, new Vector2(0, 40)))
                        {
                            _selectedReceiverIndex = i;
                            _selectedReceiver = receiver;
                        }

                        // Right-click context menu
                        var ioReceiver = ImGui.GetIO();
                        bool ctrlShiftHeldReceiver = ioReceiver.KeyCtrl && ioReceiver.KeyShift;

                        if (ImGui.BeginPopupContextItem($"##ReceiverContext{i}"))
                        {
                            using (ImRaii.Disabled(!ctrlShiftHeldReceiver))
                            {
                                if (ImGui.MenuItem("Delete"))
                                {
                                    receivers.RemoveAt(i);
                                    Configuration.Instance.Save();
                                    if (_selectedReceiverIndex == i)
                                    {
                                        _selectedReceiverIndex = -1;
                                        _selectedReceiver = null;
                                    }
                                    ImGui.EndPopup();
                                    return;
                                }
                            }

                            if (!ctrlShiftHeldReceiver && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            {
                                ImGui.SetTooltip("Hold CTRL and Shift to delete");
                            }

                            ImGui.EndPopup();
                        }

                        if (ImGui.IsItemVisible())
                        {
                            var cursorPos = ImGui.GetCursorPos();
                            ImGui.SetCursorPos(new Vector2(cursorPos.X, cursorPos.Y - 42.5f));

                            ImGui.TextUnformatted($"{receiver.ReceivingPlayer?.FullName ?? "Unknown Character"}");
                            ImGui.TextColored(new Vector4(0.6f, 0.8f, 1.0f, 1.0f),
                              $"{receiver.DestinationType} | {receiver.Mannequins.Count} Mannequin(s)");

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

        using (ImRaii.Disabled(_selectedReceiver == null))
        {
            if (ImGui.Button("Select This Receiver", new Vector2(buttonWidth, 25)))
            {
                if (_selectedReceiver != null)
                {
                    _currentMode = UIMode.ReceiverConfiguration;
                    _selectedMannequin = null;
                    _selectedMannequinIndex = -1;
                }
            }
        }

        ImGui.SameLine();

        var io = ImGui.GetIO();
        bool ctrlShiftHeld2 = io.KeyCtrl && io.KeyShift;

        using (ImRaii.Disabled(_selectedReceiver == null || !ctrlShiftHeld2))
        {
            if (ImGui.Button("Delete", new Vector2(buttonWidth, 25)))
            {
                if (_selectedReceiver != null)
                {
                    Configuration.Instance.Receivers.RemoveAt(_selectedReceiverIndex);
                    Configuration.Instance.Save();
                    _selectedReceiver = null;
                    _selectedReceiverIndex = -1;
                }
            }
        }

        if (_selectedReceiver != null && !ctrlShiftHeld2 && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Hold CTRL and Shift to delete");
    }
}
