using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using GilTransferer.Enums;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Numerics;

namespace GilTransferer.UI;

public partial class MainWindow
{
    private void DrawConfigSlotsTab()
    {
        if (_selectedScenario == null)
            return;

        using (ImRaii.Child("##ConfigSlotsChild", new Vector2(-1, -1), false))
        {
            ImGui.TextUnformatted("Global Item Configuration for Slots");
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Configure which items to use for each equipment slot.");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            var itemSheet = ExcelSheetHelper.GetSheet<Item>();
            if (itemSheet == null)
            {
                ImGui.TextColored(new Vector4(1.0f, 0.3f, 0.3f, 1.0f), "Failed to load item data.");
                return;
            }

            foreach (SlotType slotType in Enum.GetValues<SlotType>())
            {
                using (ImRaii.PushId((int)slotType))
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted($"{slotType}:");
                    ImGui.SameLine();

                    bool hasItem = Configuration.Instance.ItemsPerSlot.TryGetValue(slotType, out uint itemId);

                    if (hasItem && itemId > 0)
                    {
                        var item = itemSheet.GetRowOrDefault(itemId);

                        if (item != null)
                        {
                            var iconId = item.Value.Icon;
                            var texture = NoireService.TextureProvider.GetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup(iconId));

                            if (texture != null && texture.TryGetWrap(out var wrap, out _))
                            {
                                var iconSize = new Vector2(32, 32);
                                ImGui.Image(wrap.Handle, iconSize);

                                using (var contex = ImRaii.ContextPopupItem($"##ItemContext{slotType}"))
                                {
                                    if (contex)
                                    {
                                        var itemName = item.Value.Name.ExtractText();
                                        ImGui.TextUnformatted($"Item: {itemName}");
                                        ImGui.Separator();

                                        if (ImGui.MenuItem("Remove Item"))
                                        {
                                            Configuration.Instance.ItemsPerSlot.Remove(slotType);
                                            Configuration.Instance.Save();
                                        }
                                    }
                                }

                                if (ImGui.IsItemHovered())
                                {
                                    var itemName = item.Value.Name.ExtractText();
                                    ImGui.SetTooltip($"{itemName}\nRight-click to remove");
                                }
                            }
                            else
                            {
                                ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.2f, 1.0f), "[Icon N/A]");
                            }
                        }
                        else
                        {
                            ImGui.TextColored(new Vector4(1.0f, 0.3f, 0.3f, 1.0f), $"[Unknown Item: {itemId}]");

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip("Right-click to remove");
                            }

                            using (var contex = ImRaii.ContextPopupItem($"##ItemContext{slotType}"))
                            {
                                if (contex)
                                {
                                    if (ImGui.MenuItem("Remove Invalid Item"))
                                    {
                                        Configuration.Instance.ItemsPerSlot.Remove(slotType);
                                        Configuration.Instance.Save();
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "[Not Configured]");
                    }
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextWrapped("Tip: Right-click on an item to add it to a slot.");
        }
    }
}
