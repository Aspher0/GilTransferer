using Dalamud.Game.Command;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Inventory;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Utility;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using GilTransferer.Enums;
using GilTransferer.Helpers;
using GilTransferer.UI;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;

namespace GilTransferer;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    private const string CommandName = "/gilt";

    public readonly WindowSystem WindowSystem = new("GilTransferer");
    private MainWindow MainWindow { get; init; }
    private DebugWindow DebugWindow { get; init; }

    public Plugin()
    {
        ECommonsMain.Init(PluginInterface, this);
        NoireLibMain.Initialize(PluginInterface, this);

        Service.Initialize();

        MainWindow = new MainWindow();
        DebugWindow = new DebugWindow();

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(DebugWindow);

        NoireService.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the interface"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        NoireService.ContextMenu.OnMenuOpened += OnMenuOpened;
    }
    public void ToggleMainUi() => MainWindow.Toggle();

    private void OnCommand(string command, string args)
    {
        var argsList = args.Split(' ');
#if DEBUG
        if (args.Length > 0 && argsList[0].ToLowerInvariant() == "debug")
            DebugWindow.Toggle();
        else
#endif
            MainWindow.Toggle();
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        uint? baseItemId = null;
        var itemSheet = ExcelSheetHelper.GetSheet<Item>();

        if (itemSheet == null)
            return;

        switch (args.MenuType)
        {
            case ContextMenuType.Default:
                {
                    if (args.Target is not MenuTargetDefault target ||
                        target.TargetContentId != 0)
                        return;

                    unsafe
                    {
                        switch (args.AddonName)
                        {
                            case "ChatLog":
                                {
                                    var agentInstance = AgentChatLog.Instance();
                                    var itemId = AgentChatLog.Instance()->ContextItemId;
                                    baseItemId = ItemUtil.GetBaseId(itemId).ItemId;
                                    break;

                                }
                            default:
                                {
                                    // Sometimes it doesnt fucking work, no idea why, it just returns 0, fuck you GameGui
                                    var guiHoveredItem = NoireService.GameGui.HoveredItem;
                                    var itemId = ItemUtil.GetBaseId((uint)guiHoveredItem).ItemId;
                                    baseItemId = ItemUtil.GetBaseId(itemId).ItemId;
                                    break;
                                }
                        }
                    }

                    break;
                }
            case ContextMenuType.Inventory:
                {
                    var targetItem = (args.Target as MenuTargetInventory)!.TargetItem;

                    if (targetItem is not GameInventoryItem)
                        return;

                    var gameInventoryItem = (GameInventoryItem)targetItem;
                    baseItemId = gameInventoryItem.BaseItemId;
                    break;

                }
            default:
                return;
        }

        if (baseItemId == null || baseItemId == 0)
            return;

        Item? item = itemSheet.GetRowOrDefault(baseItemId.Value);

        if (item == null)
            return;

        var itemName = item.Value.Name.ExtractText();

        if (itemName.IsNullOrWhitespace())
            return;

        var equipSlotCategory = item.Value.EquipSlotCategory.Value;

        if (!(equipSlotCategory.RowId switch
        {
            0 => false, // not equippable
            2 when item.Value.FilterGroup != 3 => false, // any OffHand that's not a Shield
            6 => false, // Waist
            17 => false, // SoulCrystal
            _ => true
        }))
        {
            return;
        }

        var slotType = CommonHelper.GetSlotTypeFromEquipSlotCategory(equipSlotCategory.RowId);
        if (slotType == null)
            return;

        bool isRing = slotType == SlotType.RingRight;

        args.AddMenuItem(new()
        {
            Name = $"Set {itemName.ShortenString(15)} to slot {(isRing ? "Ring" : slotType.ToString())}",
            Prefix = SeIconChar.BoxedLetterG,
            PrefixColor = 561,
            IsSubmenu = isRing,
            OnClicked = (IMenuItemClickedArgs a) =>
            {
                if (isRing)
                {
                    a.OpenSubmenu(new MenuItem[]
                    {
                        new()
                        {
                            Name = "Add to Right Ring",
                            IsEnabled = true,
                            OnClicked = (IMenuItemClickedArgs b) =>
                            {
                                Configuration.Instance.ItemsPerSlot[SlotType.RingRight] = item.Value.RowId;
                                Configuration.Instance.Save();
                            },
                        },
                        new()
                        {
                            Name = "Add to Left Ring",
                            IsEnabled = true,
                            OnClicked = (IMenuItemClickedArgs b) =>
                            {
                                Configuration.Instance.ItemsPerSlot[SlotType.RingLeft] = item.Value.RowId;
                                Configuration.Instance.Save();
                            },
                        },
                    });
                }
                else
                {
                    Configuration.Instance.ItemsPerSlot[slotType.Value] = item.Value.RowId;
                    Configuration.Instance.Save();
                }
            },
        });
    }

    public void Dispose()
    {
        NoireService.ContextMenu.OnMenuOpened -= OnMenuOpened;

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        MainWindow.Dispose();
        DebugWindow.Dispose();

        NoireService.CommandManager.RemoveHandler(CommandName);

        Service.Dispose();

        NoireLibMain.Dispose();
        ECommonsMain.Dispose();
    }
}
