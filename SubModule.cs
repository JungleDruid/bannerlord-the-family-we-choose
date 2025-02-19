using System;
using System.IO;
using System.Reflection;
using Bannerlord.ButterLib.Common.Extensions;
using HarmonyLib;
using JetBrains.Annotations;
using MCM.Abstractions.Base.Global;
using MCM.Abstractions.Base.PerCampaign;
using MCM.Abstractions.FluentBuilder;
using Microsoft.Extensions.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TheFamilyWeChoose.Patches;

#if DEBUG
using TheFamilyWeChoose.Utils;
using TaleWorlds.InputSystem;
#endif

namespace TheFamilyWeChoose;

public class SubModule : MBSubModuleBase
{
    public static readonly string Name = typeof(SubModule).Namespace!;
    public static readonly Version Version = typeof(SubModule).Assembly.GetName().Version;
    public static readonly Harmony Harmony = new("Bannerlord.TheFamilyWeChoose");
    private ILogger _logger;
    internal ILogger Logger => _logger ??= LogFactory.Get<SubModule>();
    public static SubModule Instance { get; private set; }
    [CanBeNull] private FluentGlobalSettings _fluentGlobalSettings;
    [CanBeNull] private FluentPerCampaignSettings _fluentPerCampaignSettings;
    private Settings GlobalSettings { get; } = new();
    internal Settings CampaignSettings { get; private set; }
    private bool _hasBannerKings;

    private void OnServiceRegistration()
    {
        this.AddSerilogLoggerProvider($"{Name}.log", [$"{Name}.*"], o => o.MinimumLevel.Verbose());
    }

    protected override void OnSubModuleLoad()
    {
        Instance = this;
        OnServiceRegistration();
        Harmony.PatchAll(Assembly.GetExecutingAssembly());
    }

#if DEBUG
    private bool _debugStart = false;
#endif

    protected override void OnBeforeInitialModuleScreenSetAsRoot()
    {
#if DEBUG
        if (_debugStart) return;
#endif
        Logger.LogInformation($"{Name} {Version} starting up...");
        try
        {
            _hasBannerKings = BannerKingsPatches.PatchAll(Harmony);
        }
        catch (FileNotFoundException)
        {
        }

        _fluentGlobalSettings = MCMSettings.AddSettings(GlobalSettings, _hasBannerKings).BuildAsGlobal();
        _fluentGlobalSettings.Register();
    }

    protected override void OnApplicationTick(float dt)
    {
#if DEBUG
        bool superKey = (Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl)) &&
                        (Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift)) &&
                        (Input.IsKeyDown(InputKey.LeftAlt) || Input.IsKeyDown(InputKey.RightAlt));

        if (superKey && Input.IsKeyPressed(InputKey.B))
        {
            SafeDebugger.Break();
        }

        if (superKey && Input.IsKeyPressed(InputKey.P))
        {
            SafeDebugger.Break();
            _debugStart = false;
            OnBeforeInitialModuleScreenSetAsRoot();
        }

        if (superKey && Input.IsKeyPressed(InputKey.O))
        {
            _logger = null;
            _logger.IsEnabled(LogLevel.Critical);
            // refresh MCM menu
            if (_fluentGlobalSettings is null) return;
            _fluentGlobalSettings.Unregister();
            _fluentGlobalSettings = MCMSettings.AddSettings(GlobalSettings, _hasBannerKings).BuildAsGlobal();
            _fluentGlobalSettings.Register();
            if (_fluentPerCampaignSettings is null) return;
            _fluentPerCampaignSettings.Unregister();
            ISettingsBuilder builder = MCMSettings.AddSettings(CampaignSettings, _hasBannerKings);
            _fluentPerCampaignSettings = builder.BuildAsPerCampaign();
            _fluentPerCampaignSettings.Register();
        }
#endif
    }

    public override void OnAfterGameInitializationFinished(Game game, object starterObject)
    {
        if (game.GameType is not Campaign) return;
        _fluentPerCampaignSettings?.Unregister();
        _fluentGlobalSettings?.Unregister();
        CampaignSettings = new Settings(GlobalSettings);
        ISettingsBuilder builder = MCMSettings.AddSettings(CampaignSettings, _hasBannerKings);
        _fluentPerCampaignSettings = builder.BuildAsPerCampaign();
        _fluentPerCampaignSettings?.Register();
    }

    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        if (gameStarterObject is not CampaignGameStarter campaignGameStarter) return;
        if (_hasBannerKings)
        {
            BannerKingsPatches.AddDialogues(campaignGameStarter);
        }
    }

    public override void OnGameEnd(Game game)
    {
        if (game.GameType is not Campaign) return;
        _fluentGlobalSettings?.Register();
    }
}