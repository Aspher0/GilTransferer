using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using GilTransferer.Enums;
using GilTransferer.Models;
using NoireLib;
using NoireLib.EventBus;
using NoireLib.Helpers;
using NoireLib.TaskQueue;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace GilTransferer.UI;

public partial class MainWindow : Window, IDisposable
{
    private enum UIMode
    {
        ScenarioSelection,
        ScenarioConfiguration
    }

    private UIMode _currentMode = UIMode.ScenarioSelection;
    private Scenario? _selectedScenario = null;
    private int _selectedScenarioIndex = -1;
    private Mannequin? _selectedMannequin = null;
    private int _selectedMannequinIndex = -1;

    // Search filters for each slot type combo box
    private Dictionary<SlotType, string> _slotSearchFilters = new();

    public MainWindow() : base("Gil Transferer###GilTransferer", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(725, 600),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        TitleBarButtons = new()
        {
            new()
            {
                Click = (c) => { SystemHelper.OpenUrl("https://ko-fi.com/aspher0"); },
                Icon = FontAwesomeIcon.Coffee,
                ShowTooltip = () => { ImGui.SetTooltip("Support me on Ko-fi, Make me Happy\nThank you ♥"); },
                IconOffset = new Vector2(2, 2),
            },
        };

        var eventBus = NoireLibMain.GetModule<NoireEventBus>("EventBus")!;
        // Will auto dispose
        eventBus.Subscribe<QueueStartedEvent>((@event) => ProcessTitleChange(), owner: this);
        eventBus.Subscribe<TaskStartedEvent>((@event) => ProcessTitleChange(), owner: this);
        eventBus.Subscribe<QueueStoppedEvent>((@event) => ProcessTitleChange(), owner: this);
    }

    public void ProcessTitleChange()
    {
        string currentTaskName = GetCurrentTaskName();
        WindowName = $"Gil Transferer{currentTaskName}###GilTransferer";
    }

    private string GetCurrentTaskName()
    {
        if (!Service.TaskQueue.IsQueueProcessing())
            return string.Empty;

        var currentTask = Service.TaskQueue.GetCurrentTask();
        if (currentTask == null)
            return string.Empty;

        if (currentTask.CustomId != null)
            return $" - {currentTask.CustomId}";

        if (currentTask.PostCompletionDelay.HasValue)
            return $" - Wait {currentTask.PostCompletionDelay!.Value.TotalMilliseconds}ms";

        return string.Empty;
    }

    public override void Draw()
    {
        if (_currentMode == UIMode.ScenarioSelection)
        {
            DrawScenarioSelectionMode();
        }
        else if (_currentMode == UIMode.ScenarioConfiguration)
        {
            DrawScenarioConfigurationMode();
        }
    }

    public void Dispose() { }
}
