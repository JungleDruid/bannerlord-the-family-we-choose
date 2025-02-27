using HarmonyLib;
using MCM.Abstractions.FluentBuilder;
using MCM.Common;
using TaleWorlds.Localization;

namespace TheFamilyWeChoose;

internal static class MCMSettings
{
    private static string SettingsId => SubModule.Name;
    private static string SettingsName => $"{new TextObject("{=TFWCTitle}The Family We Choose")} {SubModule.Version.ToString(3)}";

    public static ISettingsBuilder AddSettings(Settings settings, bool hasBannerKings)
    {
        ISettingsBuilder settingsBuilder = BaseSettingsBuilder
            .Create(SettingsId, SettingsName)!
            .SetFormat("json2")
            .SetFolderName(SubModule.Name)
            .CreateGroup("{=TFWCMain}Main", BuildMainGroup);

        if (hasBannerKings) settingsBuilder.CreateGroup("Banner Kings", BuildBannerKingsGroup);

        return settingsBuilder;

        void BuildMainGroup(ISettingsPropertyGroupBuilder builder)
            => builder
                .SetGroupOrder(0)
                .AddDropdown("sexual_orientation", "{=TFWCSexualOrientation}Sexual Orientation", 0,
                    new StorageRef(settings.SexualOrientationDropdown),
                    propBuilder => propBuilder.SetOrder(0))
                .AddBool("remove_cooldown", "{=TFWCRemoveCooldown}Remove Cooldown", new PropertyRef(typeof(Settings).Property("RemoveCooldown"), settings),
                    propBuilder => propBuilder.SetOrder(1).SetHintText("{=TFWCRemoveCooldownHint}Remove cooldown between courtships."));

        void BuildBannerKingsGroup(ISettingsPropertyGroupBuilder builder)
            => builder
                .SetGroupOrder(1)
                .AddBool("allow_consort", "{=TFWCAllowConsort}Allow Consort",
                    new PropertyRef(AccessTools.Property(typeof(Settings), nameof(Settings.AllowConsort)), settings),
                    propBuilder => propBuilder.SetOrder(0).SetHintText("{=TFWCAllowConsort}Allow marrying secondary partners by default. Will be overwritten by religions."))
                .AddFloatingInteger("consort_penalty", "{=TFWCConsortPenalty}Consort Penalty", 0, 2,
                    new PropertyRef(AccessTools.Property(typeof(Settings), nameof(Settings.ConsortPenalty)), settings),
                    propBuilder => propBuilder.SetOrder(1).SetHintText("{=TFWCConsortPenaltyHint}The factor of the proposed score when they are a secondary partner."));
    }
}