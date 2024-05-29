using BepInEx;
using BepInEx.Logging;
using System.Reflection;
using MonoMod.RuntimeDetour.HookGen;
using ContentSettings.API.Attributes;
using ContentSettings.API.Settings;
using KeepCameraAfterDeath.Patches;
using MyceliumNetworking;

namespace KeepCameraAfterDeath;

[ContentWarningPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_VERSION, false)]
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class KeepCameraAfterDeath : BaseUnityPlugin
{
    const uint myceliumNetworkModId = 61812; // meaningless, as long as it is the same between all the clients
    public static KeepCameraAfterDeath Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger { get; private set; } = null!;

    public bool PlayerSettingEnableRewardForCameraReturn { get; set; }
    public int PlayerSettingMetaCoinReward { get; set; }
    public int PlayerSettingCashReward { get; set; }

    public ItemInstanceData? PreservedCameraInstanceDataForHost { get; private set; } = null;
    public (int cash, int mc)? PendingRewardForCameraReturn { get; private set; } = null;

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        HookAll();

        Logger.LogInfo($"{"CAD-MOD.KeepCameraAfterDeath"} v{"1.0.0"} has loaded!");

    }

    private void Start()
    {
        MyceliumNetwork.RegisterNetworkObject(Instance, myceliumNetworkModId);

        Logger.LogInfo($"CAD-MOD: mycelium network object registered");
    }

    void OnDestroy()
    {
        MyceliumNetwork.DeregisterNetworkObject(Instance, myceliumNetworkModId);

        Logger.LogInfo($"CAD-MOD: mycelium network object destroyed");
    }

    internal static void HookAll()
    {
        SurfaceNetworkHandlerPatch.Init();
        VideoCameraPatch.Init();
        PersistentObjectsHolderPatch.Init();
        PlayerPatch.Init();
    }

    internal static void UnhookAll()
    {
        HookEndpointManager.RemoveAllOwnedBy(Assembly.GetExecutingAssembly());
    }

    public void SetPreservedCameraInstanceDataForHost(ItemInstanceData data)
    {
        //data.TryGetEntry<VideoInfoEntry>(out VideoInfoEntry t);
        //KeepCameraAfterDeath.Logger.LogInfo("CAD-MOD: SET PRESERVED CAMERA DATA video ID: " + t != null ? t.videoID.id : "NONE");
        PreservedCameraInstanceDataForHost = data;
    }

    public void ClearPreservedCameraInstanceData()
    {
        Logger.LogInfo("CAD-MOD: clear preserved camera data");
        PreservedCameraInstanceDataForHost = null;
    }

    public void SetPendingRewardForAllPlayers()
    {
        if (!MyceliumNetwork.IsHost)
        {
            return;
        }

        Logger.LogInfo("CAD-MOD: host will try set rewards for players using RPC");

        // send out host's setting for rewards to all players
        MyceliumNetwork.RPC(myceliumNetworkModId, nameof(RPC_SetPendingRewardForCameraReturn), ReliableType.Reliable, PlayerSettingCashReward, PlayerSettingMetaCoinReward);
    }

    [CustomRPC]
    public void RPC_SetPendingRewardForCameraReturn(int cash, int mc)
    {
        KeepCameraAfterDeath.Logger.LogInfo("CAD-MOD: commanded by host to set reward for camera return: $" + cash + " and " + mc + "MC");
        PendingRewardForCameraReturn = (cash, mc);
    }

    public void ClearPendingRewardForCameraReturn()
    {
        KeepCameraAfterDeath.Logger.LogInfo("CAD-MOD: clear pending reward");
        PendingRewardForCameraReturn = null;
    }

    public void Command_ResetDataforDay()
    {
        if (!MyceliumNetwork.IsHost)
        {
            return;
        }

        Logger.LogInfo("CAD-MOD: try clear day's data for players using RPC");

        MyceliumNetwork.RPC(myceliumNetworkModId, nameof(RPC_ResetDataforDay), ReliableType.Reliable);
    }

    [CustomRPC]
    public void RPC_ResetDataforDay()
    {
        // Clear any camera film that was preserved from the lost world on the previous day
        // Clear pending rewards for camera return
        KeepCameraAfterDeath.Logger.LogInfo("CAD-MOD: commanded by host to clear today's data");
        KeepCameraAfterDeath.Instance.ClearData();
    }

    public void ClearData()
    {
        // Clear any camera film that was preserved from the lost world on the previous day
        // Clear pending rewards for camera return
        KeepCameraAfterDeath.Instance.ClearPreservedCameraInstanceData();
        KeepCameraAfterDeath.Instance.ClearPendingRewardForCameraReturn();
    }

    [SettingRegister("KeepCameraAfterDeath Mod Settings")]
    public class EnableRewardForCameraReturnSetting : BoolSetting, ICustomSetting
    {
        public override void ApplyValue()
        {
            //KeepCameraAfterDeath.Logger.LogInfo($"MC Reward for camera return: {Value}");
            KeepCameraAfterDeath.Instance.PlayerSettingEnableRewardForCameraReturn = Value;
        }

        public string GetDisplayName() => "Turn on incentives for bringing the camera back to the surface (uses the host's game settings)";

        protected override bool GetDefaultValue() => true;
    }

    [SettingRegister("KeepCameraAfterDeath Mod Settings")]
    public class SetMetaCoinRewardForCameraReturnSetting : IntSetting, ICustomSetting
    {
        public override void ApplyValue()
        {
            //KeepCameraAfterDeath.Logger.LogInfo($"Meta Coin (MC) reward for camera return: {Value}");
            KeepCameraAfterDeath.Instance.PlayerSettingMetaCoinReward = Value;
        }

        public string GetDisplayName() => "Meta Coin (MC) reward for camera return (uses the host's game settings)";

        protected override int GetDefaultValue() => 10;

        override protected (int, int) GetMinMaxValue() => (0, 100);
    }

    [SettingRegister("KeepCameraAfterDeath Mod Settings")]
    public class SetCashRewardForCameraReturnSetting : IntSetting, ICustomSetting
    {
        public override void ApplyValue()
        {
            //KeepCameraAfterDeath.Logger.LogInfo($"Cash reward for camera return: {Value}");
            KeepCameraAfterDeath.Instance.PlayerSettingCashReward = Value;
        }

        public string GetDisplayName() => "Cash reward for camera return (uses the host's game settings)";

        protected override int GetDefaultValue() => 0;

        override protected (int, int) GetMinMaxValue() => (0, 1000);
    }
}
