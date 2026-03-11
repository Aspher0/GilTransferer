using Dalamud.Plugin;
using ECommons;
using ECommons.EzIpcManager;
using NoireLib;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace GilTransferer.IPC;

public enum ResidentialAetheryteKind
{
    Uldah = 9,
    Gridania = 2,
    Limsa = 8,
    Foundation = 70,
    Kugane = 111,
}

public enum PropertyType
{
    House, Apartment
}

public enum HouseEnterMode
{
    None, Walk_to_door, Enter_house, Enter_workshop
}

public class LifestreamIPC
{
    public LifestreamIPC()
    {
        EzIPC.Init(this, "Lifestream");
    }

    [EzIPC("Instance")] public Func<IDalamudPlugin> _instance;

    public bool IsAvailable()
    {
        try
        {
            var instance = _instance.Invoke();
            return instance != null;
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(this, ex, "Lifestream IPC instance check failed.");
            return false;
        }
    }

    [EzIPC("ExecuteCommand")] public Action<string> _executeCommand;

    public void ExecuteCommand(string command)
    {
        if (!IsAvailable())
            return;
        _executeCommand?.Invoke(command);
    }

    [EzIPC("IsBusy")] public Func<bool> _isBusy;

    public bool IsBusy()
    {
        if (!IsAvailable())
            return false;
        return _isBusy?.Invoke() ?? false;
    }

    [EzIPC("Abort")] public Action _abort;

    public void Abort()
    {
        if (!IsAvailable())
            return;
        _abort?.Invoke();
    }

    [EzIPC("ChangeWorld")] public Func<string, bool> _changeWorld;

    public bool ChangeWorld(string worldName)
    {
        if (!IsAvailable())
            return false;
        return _changeWorld?.Invoke(worldName) ?? false;
    }

    [EzIPC("Move")] public Action<List<Vector3>> _move;

    public void Move(List<Vector3> path)
    {
        if (!IsAvailable())
            return;
        _move?.Invoke(path);
    }

    [EzIPC("ChangeCharacter")] public Func<string, string, ErrorCode?> _changeCharacter;

    public ErrorCode? ChangeCharacter(string characterName, string worldName)
    {
        if (!IsAvailable())
            return null;
        return _changeCharacter?.Invoke(characterName, worldName) ?? null;
    }

    [EzIPC("ConnectAndLogin")] public Func<string, string, bool> _connectAndLogin;

    public bool ConnectAndLogin(string characterName, string worldName)
    {
        if (!IsAvailable())
            return false;
        return _connectAndLogin?.Invoke(characterName, worldName) ?? false;
    }
}
