using ECommons.EzIpcManager;
using NoireLib;
using System;
using System.Diagnostics.CodeAnalysis;

namespace GilTransferer.IPC;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public sealed class ExternalTerritoryConfig
{
#pragma warning disable CS0414 // Field is assigned but its value is never used
    public bool? EnableQuestAccept = true;
    public bool? EnableQuestComplete = true;
    public bool? EnableRewardPick = true;
    public bool? EnableRequestHandin = true;
    public bool? EnableCutsceneEsc = true;
    public bool? EnableCutsceneSkipConfirm = true;
    public bool? EnableTalkSkip = true;
    public bool? EnableRequestFill = true;
    public bool? EnableAutoInteract = false;
#pragma warning restore CS0414 // Field is assigned but its value is never used
}

public class TextAdvanceIPC
{
    public TextAdvanceIPC()
    {
        EzIPC.Init(this, "TextAdvance");
    }

    [EzIPC("IsEnabled")] public Func<bool> _isEnabled;

    public bool IsEnabled()
    {
        try
        {
            return _isEnabled.Invoke();
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(this, ex, "Error checking if TextAdvance is enabled via IPC.");
            return false;
        }
    }

    public bool IsActive()
    {
        try
        {
            bool isEnabled = _isEnabled.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            NoireLogger.LogError(this, ex, "Error checking if TextAdvance is active via IPC.");
            return false;
        }
    }

    [EzIPC("EnableExternalControl")] public Func<string, ExternalTerritoryConfig, bool> _enableExternalControl;

    public bool EnableExternalControl(ExternalTerritoryConfig config)
    {
        if (IsActive())
            return _enableExternalControl.Invoke("GilTransferer", config);
        return false;
    }

    [EzIPC("DisableExternalControl")] public Func<string, bool> _disableExternalControl;

    public bool DisableExternalControl()
    {
        if (IsActive())
            return _disableExternalControl.Invoke("GilTransferer");
        return false;
    }

    [EzIPC("IsInExternalControl")] public Func<bool> _isInExternalControl;

    public bool IsInExternalControl()
    {
        if (IsActive())
            return _isInExternalControl.Invoke();
        return false;
    }
}
