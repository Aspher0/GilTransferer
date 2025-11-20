using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using GilTransferer.Enums;
using GilTransferer.Models;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace GilTransferer.UI;

public partial class MainWindow : Window, IDisposable
{
    private enum UIMode
    {
        ReceiverSelection,
        ReceiverConfiguration
    }

    private UIMode _currentMode = UIMode.ReceiverSelection;
    private Receiver? _selectedReceiver = null;
    private int _selectedReceiverIndex = -1;
    private Mannequin? _selectedMannequin = null;
    private int _selectedMannequinIndex = -1;

    // Search filters for each slot type combo box
    private Dictionary<SlotType, string> _slotSearchFilters = new();

    public MainWindow()
    : base("Gil Transferer###GilTransferer", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(725, 600),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void Draw()
    {
        if (_currentMode == UIMode.ReceiverSelection)
        {
            DrawReceiverSelectionMode();
        }
        else if (_currentMode == UIMode.ReceiverConfiguration)
        {
            DrawReceiverConfigurationMode();
        }
    }

    public void Dispose() { }
}
