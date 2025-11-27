using AutoRetainerAPI;
using AutoRetainerAPI.Configuration;
using GilTransferer.IPC;
using NoireLib;
using NoireLib.EventBus;
using NoireLib.Helpers;
using NoireLib.Helpers.ObjectExtensions;
using NoireLib.TaskQueue;
using System.Collections.Generic;

namespace GilTransferer;

public static class Service
{
    public static NoireEventBus EventBus { get; private set; }
    public static NoireTaskQueue TaskQueue { get; private set; }

    public static AutoRetainerApi AutoRetainerAPI { get; set; } = new();
    public static LifestreamIPC LifestreamIPC { get; set; } = new();
    public static TextAdvanceIPC TextAdvanceIPC { get; set; } = new();

    private static List<ulong> _cachedRegisteredCharacters = new();
    public static List<ulong> RegisteredCharacters
    {
        get
        {
            var result = ThrottleHelper.Throttle("RegisteredCharacters", () => AutoRetainerAPI.GetRegisteredCharacters(), intervalMilliseconds: 500);
            if (!result.IsDefault())
                _cachedRegisteredCharacters = result!;
            return _cachedRegisteredCharacters;
        }
    }

    private static Dictionary<ulong, OfflineCharacterData> _cachedOfflineCharacterData = new();
    public static OfflineCharacterData? GetOfflineCharacterData(ulong cid)
    {
        var result = ThrottleHelper.Throttle($"OfflineCharacterData_{cid}", () => AutoRetainerAPI.GetOfflineCharacterData(cid), intervalMilliseconds: 500);
        if (!result.IsDefault())
            _cachedOfflineCharacterData[cid] = result!;
        return _cachedOfflineCharacterData.TryGetValue(cid, out var data) ? data : null;
    }

    public static void Initialize()
    {
        EventBus = NoireLibMain.AddModule(new NoireEventBus("EventBus"))!;
        TaskQueue = NoireLibMain.AddModule(new NoireTaskQueue("TaskQueueDebug", eventBus: EventBus))!;
    }

    public static void StopQueue()
    {
        TaskQueue.StopQueue();
        TextAdvanceIPC.DisableExternalControl();
        LifestreamIPC.Move([]);
    }

    public static void Dispose()
    {
        AutoRetainerAPI.Dispose();
        StopQueue();
    }
}
