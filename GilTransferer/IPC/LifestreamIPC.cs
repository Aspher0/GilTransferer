global using AddressBookEntryTuple = (string Name, int World, int City, int Ward, int PropertyType, int Plot, int Apartment, bool ApartmentSubdivision, bool AliasEnabled, string Alias);
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

    [EzIPC("BuildAddressBookEntry")] public Func<string, string, string, string, bool, bool, AddressBookEntryTuple> _buildAddressBookEntry;

    public AddressBookEntryTuple? BuildAddressBookEntry(string worldStr, string cityStr, string wardNum, string plotApartmentNum, bool isApartment, bool isSubdivision)
    {
        if (!IsAvailable())
            return null;
        return _buildAddressBookEntry.Invoke(worldStr, cityStr, wardNum, plotApartmentNum, isApartment, isSubdivision);
    }

    [EzIPC("IsHere")] public Func<AddressBookEntryTuple, bool> _isHere;

    public bool IsHere(AddressBookEntryTuple entry)
    {
        if (!IsAvailable())
            return false;
        return _isHere?.Invoke(entry) ?? false;
    }

    [EzIPC("IsQuickTravelAvailable")] public Func<AddressBookEntryTuple, bool> _isQuickTravelAvailable;

    public bool IsQuickTravelAvailable(AddressBookEntryTuple entry)
    {
        if (!IsAvailable())
            return false;
        return _isQuickTravelAvailable?.Invoke(entry) ?? false;
    }

    [EzIPC("GoToHousingAddress")] public Action<AddressBookEntryTuple> _goToHousingAddress;

    public void GoToHousingAddress(AddressBookEntryTuple entry)
    {
        if (!IsAvailable())
            return;
        _goToHousingAddress?.Invoke(entry);
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

    [EzIPC("GetCurrentPlotInfo")] public Func<(ResidentialAetheryteKind Kind, int Ward, int Plot)?> _getCurrentPlotInfo;

    public (ResidentialAetheryteKind Kind, int Ward, int Plot)? GetCurrentPlotInfo()
    {
        if (!IsAvailable())
            return null;
        return _getCurrentPlotInfo?.Invoke();
    }

    [EzIPC("GetPlotEntrance")] public Func<uint, int, Vector3?> _getPlotEntrance;

    public Vector3? GetPlotEntrance(uint territory, int plot)
    {
        if (!IsAvailable())
            return null;
        return _getPlotEntrance?.Invoke(territory, plot);
    }

    [EzIPC("EnqueuePropertyShortcut")] public Action<PropertyType, HouseEnterMode> _enqueuePropertyShortcut;

    public void EnqueuePropertyShortcut(PropertyType propertyType, HouseEnterMode enterMode)
    {
        if (!IsAvailable())
            return;
        _enqueuePropertyShortcut?.Invoke(propertyType, enterMode);
    }

    [EzIPC("Move")] public Action<List<Vector3>> _move;

    public void Move(List<Vector3> path)
    {
        if (!IsAvailable())
            return;
        _move?.Invoke(path);
    }

    [EzIPC("GetRealTerritoryType")] public Func<uint> _getRealTerritoryType;

    public uint GetRealTerritoryType()
    {
        if (!IsAvailable())
            return 0;
        return _getRealTerritoryType?.Invoke() ?? 0;
    }

    [EzIPC("Logout")] public Func<ErrorCode> _logout;

    public ErrorCode? Logout()
    {
        if (!IsAvailable())
            return null;
        return _logout?.Invoke() ?? null;
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

    [EzIPC("ConnectAndTravel")] public Func<string, string, string, bool, bool> _connectAndTravel;

    public bool ConnectAndTravel(string characterName, string characterHomeworld, string destination, bool noLogin)
    {
        if (!IsAvailable())
            return false;
        return _connectAndTravel?.Invoke(characterName, characterHomeworld, destination, noLogin) ?? false;
    }

    [EzIPC("CanAutoLogin")] public Func<bool> _canAutoLogin;

    public bool CanAutoLogin()
    {
        if (!IsAvailable())
            return false;
        return _canAutoLogin?.Invoke() ?? false;
    }
}
