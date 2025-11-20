using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.MathHelpers;
using GilTransferer.Enums;
using Lumina.Excel.Sheets;
using NoireLib;
using NoireLib.Helpers;
using System;
using System.Numerics;

namespace GilTransferer.UI;

public class DebugWindow : Window, IDisposable
{
    public DebugWindow()
        : base("Gil Transferer Debug###SingleAccountMannequinGilTransfertDebug", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void Draw()
    {
        ImGui.Spacing();

        var localPlayer = NoireService.ClientState.LocalPlayer;
        if (localPlayer == null)
        {
            ImGui.TextUnformatted("Our local player is currently not loaded.");
        }
        else
        {
            var territoryId = NoireService.ClientState.TerritoryType;
            if (ExcelSheetHelper.GetSheet<TerritoryType>()?.TryGetRow(territoryId, out var territoryRow) ?? false)
            {
                ImGui.TextUnformatted($"We are currently in ({territoryId}) \"{territoryRow.PlaceName.Value.Name}\"");
            }
            else
            {
                ImGui.TextUnformatted("Invalid territory.");
            }
        }

        ImGui.Spacing();

        var target = NoireService.TargetManager.Target;

        if (target is INpc targetNpc && targetNpc.ObjectKind == ObjectKind.EventNpc)
        {
            ImGui.TextUnformatted($"Our current target is NPC");
        }
        else if (target != null)
        {
            var isEstateEntrance = target.BaseId == (uint)EntranceType.EstateEntrance;
            var isApartmentEntrance = target.BaseId == (uint)EntranceType.ApartmentEntrance;

            switch (target.BaseId)
            {
                case (uint)EntranceType.EstateEntrance:
                    ImGui.TextUnformatted("Target is an Estate Entrance.");
                    break;
                case (uint)EntranceType.ApartmentEntrance:
                    ImGui.TextUnformatted("Target is an Apartment Entrance.");
                    break;
                case (uint)EntranceType.WorkshopEntrance:
                    ImGui.TextUnformatted("Target is a Workshop Entrance.");
                    break;
                default:
                    ImGui.TextUnformatted("Target is not a valid entrance.");
                    break;
            }
        }
        else
        {
            ImGui.TextUnformatted("Target invalid.");
        }

        ImGui.Spacing();

        if (Service.AutoRetainerAPI.Ready)
        {
            using (var child = ImRaii.Child("##AutoRetainerAPIChild", new Vector2(-1, 200), true))
            {
                var CIDs = Service.RegisteredCharacters;

                foreach (var cid in CIDs)
                {
                    var charData = Service.GetOfflineCharacterData(cid);
                    if (charData == null)
                        continue;

                    ImGui.TextWrapped($"Character: {charData.Name} has {charData.Gil} gil(s)");
                }
            }
        }

        var isAvailable = Service.LifestreamIPC.IsAvailable();
        ImGui.TextUnformatted($"Lifestream IPC Instance: {(isAvailable ? "Available" : "Unavailable")}");

        if (isAvailable)
        {
            try
            {
                var isBusy = Service.LifestreamIPC.IsBusy();
                ImGui.TextUnformatted($"Lifestream IPC Busy: {(isBusy ? "Yes" : "No")}");

                var currentPlotInfos = Service.LifestreamIPC.GetCurrentPlotInfo();
                if (currentPlotInfos != null)
                {
                    ImGui.TextUnformatted($"Lifestream IPC Current Plot Info: {currentPlotInfos.Value.Kind} W{currentPlotInfos.Value.Ward + 1} P{currentPlotInfos.Value.Plot + 1}");
                }

                if (localPlayer != null)
                {
                    // Get forward vector
                    var characterPosition = localPlayer.Position;
                    var characterRotation = localPlayer.Rotation;
                    var characterForward = ECommons.MathHelpers.MathHelper.GetPointFromAngleAndDistance(characterPosition.ToVector2(), characterRotation, 1);
                    ImGui.TextUnformatted($"New Position (1 yalm forward): X:{characterForward.X} Y:{localPlayer.Position.Y} Z:{characterForward.Y}");

                    if (ImGui.Button("Move forward 1 yalm"))
                        Service.LifestreamIPC.Move([new(characterForward.X, localPlayer.Position.Y, characterForward.Y)]);
                }
            }
            catch (Exception ex)
            {
                NoireLogger.LogError(this, ex, "Lifestream IPC Error: ");
                throw;
            }
        }
    }

    public void Dispose() { }
}
