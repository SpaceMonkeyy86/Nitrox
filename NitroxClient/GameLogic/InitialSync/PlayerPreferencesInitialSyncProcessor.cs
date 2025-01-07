using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NitroxClient.Communication;
using NitroxClient.GameLogic.InitialSync.Abstract;
using NitroxClient.MonoBehaviours;
using NitroxClient.Unity.Helper;
using NitroxModel.DataStructures.GameLogic;
using NitroxModel.Packets;

namespace NitroxClient.GameLogic.InitialSync;

public sealed class PlayerPreferencesInitialSyncProcessor : InitialSyncProcessor
{
    public PlayerPreferencesInitialSyncProcessor()
    {
        // list of processors which may cause the spawn of Signal pings
        AddDependency<PlayerInitialSyncProcessor>();
        AddDependency<GlobalRootInitialSyncProcessor>();
        AddDependency<StoryGoalInitialSyncProcessor>();
        AddDependency<PdaInitialSyncProcessor>();
        AddDependency<RemotePlayerInitialSyncProcessor>();
    }

    public override List<Func<InitialPlayerSync, IEnumerator>> Steps { get; } =
    [
        UpdatePins,
        UpdatePingInstancePreferences
    ];

    private static IEnumerator UpdatePins(InitialPlayerSync packet)
    {
        using (PacketSuppressor<RecipePinned>.Suppress())
        {
            PinManager.main.Deserialize(packet.Preferences.PinnedTechTypes.Select(techType => (TechType)techType).ToList());
        }
        yield break;
    }

    private static IEnumerator UpdatePingInstancePreferences(InitialPlayerSync packet)
    {
        Dictionary<string, PingInstancePreference> pingPreferences = packet.Preferences.PingPreferences;
        void UpdateInstance(PingInstance instance)
        {
            ModifyPingInstanceIfPossible(instance, pingPreferences, () => UpdateInstance(instance));
            RefreshPingEntryInPDA(instance);
        }

        PingManager.onAdd += UpdateInstance;
        UnityEngine.Object.FindObjectsOfType<PingInstance>().ForEach(UpdateInstance);
        yield break;
    }

    /// <summary>
    /// Updates the given pingInstance if it has a specified preference
    /// </summary>
    private static void ModifyPingInstanceIfPossible(PingInstance pingInstance, Dictionary<string, PingInstancePreference> preferences, Action callback)
    {
        if (!TryGetKeyForPingInstance(pingInstance, out string pingKey, out bool isRemotePlayerPing, callback) ||
            !preferences.TryGetValue(pingKey, out PingInstancePreference preference))
        {
            return;
        }

        using (PacketSuppressor<SignalPingPreferenceChanged>.Suppress())
        {
            // We don't want to set the color for a remote player's signal
            if (!isRemotePlayerPing)
            {
                pingInstance.SetColor(preference.Color);
            }
            pingInstance.SetVisible(preference.Visible);
        }
    }

    // Right after initial sync modifications, uGUI_PingEntry elements don't show their updated state
    private static void RefreshPingEntryInPDA(PingInstance pingInstance)
    {
        if (!uGUI_PDA.main || !uGUI_PDA.main.tabs.TryGetValue(PDATab.Ping, out uGUI_PDATab pdaTab))
        {
            return;
        }
        uGUI_PingTab pingTab = pdaTab as uGUI_PingTab;
        if (pingTab && pingTab.entries.TryGetValue(pingInstance.Id, out uGUI_PingEntry pingEntry))
        {
            pingEntry.SetColor(pingInstance.colorIndex);
            pingEntry.SetVisible(pingInstance.visible);
        }
    }

    /// <summary>
    /// Retrieves the identifier of a PingInstance depending on its type and container
    /// </summary>
    /// <remarks>
    /// We need to differentiate three types of pings, the "normal pings" from objects that emit a signal, these objects generally contain a NitroxEntity
    /// Another type is Signal pings that are generated by the story events, they are located in the Global Root and don't contain a NitroxEntity, to be identified, they have another object: a SignalPing which contains a description key
    /// The last type possible is RemotePlayers' pings which are located in a GameObject that is 2 steps under the main object
    /// </remarks>
    public static bool TryGetKeyForPingInstance(PingInstance pingInstance, out string pingKey, out bool isRemotePlayerPing, Action failCallback = null)
    {
        isRemotePlayerPing = false;
        if (pingInstance.TryGetComponent(out SignalPing signalPing))
        {
            pingKey = signalPing.descriptionKey;
            // Sometimes, the SignalPing will not have loaded properly so we need to postpone the key detection
            if (pingKey == null)
            {
                pingInstance.StartCoroutine(DelayPingKeyDetection(failCallback));
                return false;
            }
            return true;
        }
        if (pingInstance.TryGetComponent(out NitroxEntity nitroxEntity))
        {
            pingKey = nitroxEntity.Id.ToString();
            return true;
        }
        if (pingInstance.transform.TryGetComponentInAscendance(2, out nitroxEntity))
        {
            pingKey = nitroxEntity.Id.ToString();
            isRemotePlayerPing = true;
            return true;
        }
        // Known issue for a ping named "xSignal(Clone)" that appears temporarily when another player joins
        if (pingInstance.name.Equals("xSignal(Clone)"))
        {
            pingKey = string.Empty;
            return false;
        }

        Log.Warn($"Couldn't find PingInstance identifier for {pingInstance.name} under {pingInstance.transform.parent}");
        pingKey = string.Empty;
        return false;
    }

    private static IEnumerator DelayPingKeyDetection(Action delayedAction)
    {
        yield return Yielders.WaitForHalfSecond;
        delayedAction?.Invoke();
    }
}
