using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.SceneInformationPopupTypes;
using TaleWorlds.Core;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace TheFamilyWeChoose.Patches;

public static class CampaignSystemPatches
{
    [HarmonyPatch(typeof(DefaultMarriageModel), nameof(DefaultMarriageModel.IsCoupleSuitableForMarriage))]
    public static class DefaultMarriageModelIsCoupleSuitableForMarriagePatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher matcher = new(instructions);

            MethodInfo isFemaleGetter = AccessTools.PropertyGetter(typeof(Hero), "IsFemale");

            matcher
                .MatchStartForward(
                    CodeMatch.LoadsArgument(),
                    CodeMatch.Calls(isFemaleGetter),
                    CodeMatch.LoadsArgument(),
                    CodeMatch.Calls(isFemaleGetter),
                    new CodeMatch(OpCodes.Beq_S));

            var labels = matcher.Labels;
            object operand = matcher.InstructionAt(4).operand;
            matcher
                .RemoveInstructions(5)
                .Insert(
                    CodeInstruction.LoadArgument(1),
                    CodeInstruction.LoadArgument(2),
                    CodeInstruction.Call(typeof(DefaultMarriageModelIsCoupleSuitableForMarriagePatch), nameof(MatchSexuality)),
                    new CodeInstruction(OpCodes.Brfalse_S, operand)
                )
                .AddLabels(labels);

            return matcher.InstructionEnumeration();
        }

        public static bool MatchSexuality(Hero hero1, Hero hero2)
        {
            if (hero1 == Hero.MainHero || hero2 == Hero.MainHero)
            {
                return SubModule.Instance.CampaignSettings.SexualOrientation switch
                {
                    SexualOrientation.Heterosexual => hero1.IsFemale != hero2.IsFemale,
                    SexualOrientation.Bisexual => true,
                    SexualOrientation.Homosexual => hero1.IsFemale == hero2.IsFemale,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            return hero1.IsFemale != hero2.IsFemale;
        }
    }

    [HarmonyPatch(typeof(RomanceCampaignBehavior), "conversation_player_can_open_courtship_on_condition")]
    public static class PlayerCanOpenCourtshipOnConditionPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher matcher = new(instructions);
            MethodInfo getIsFemale = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.IsFemale));
            MethodInfo getMainHero = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.MainHero));
            MethodInfo getOneToOneConversationHero = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.OneToOneConversationHero));

            matcher.MatchStartForward(CodeMatch.Calls(getMainHero), CodeMatch.Calls(getIsFemale));

            if (!matcher.IsValid) return matcher.InstructionEnumeration();

            matcher.Instruction.operand = getOneToOneConversationHero;
            matcher.MatchStartForward(new CodeMatch(OpCodes.Brfalse_S)).Instruction.opcode = OpCodes.Brtrue_S;
            SubModule.Instance.Logger.LogDebug("Patched MainHero.IsFemale to OneToOneConversationHero.IsFemale");

            return matcher.InstructionEnumeration();
        }
    }

    // force the main hero to be the first hero
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroesMarried))]
    public static class CampaignEventDispatcherOnHeroesMarriedPatch
    {
        public static void Prefix(ref Hero hero1, ref Hero hero2)
        {
            if (hero2 != Hero.MainHero) return;
            hero2 = hero1;
            hero1 = Hero.MainHero;
        }
    }

    // patch the second hero's equipment to be civilian equipment
    [HarmonyPatch(typeof(MarriageSceneNotificationItem), nameof(MarriageSceneNotificationItem.GetSceneNotificationCharacters))]
    public static class MarriageSceneNotificationItemGetSceneNotificationCharactersPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher matcher = new(instructions);

            matcher
                .MatchEndForward(
                    CodeMatch.Calls(AccessTools.Method(typeof(MarriageSceneNotificationItem), "GetBrideEquipmentIDFromCulture")))
                .MatchEndForward(
                    CodeMatch.Calls(AccessTools.PropertyGetter(typeof(MBEquipmentRoster), nameof(MBEquipmentRoster.DefaultEquipment))))
                .MatchStartForward(CodeMatch.StoresLocal())
                .Insert(
                    CodeInstruction.LoadArgument(0),
                    CodeInstruction.Call(typeof(MarriageSceneNotificationItemGetSceneNotificationCharactersPatch), nameof(SwitchBrideEquipment))
                );

            return matcher.InstructionEnumeration();
        }

        private static Equipment SwitchBrideEquipment(Equipment brideEquipment, MarriageSceneNotificationItem instance)
        {
            return instance.BrideHero.CivilianEquipment.Clone();
        }
    }

    [HarmonyPatch(typeof(DefaultMarriageModel), nameof(DefaultMarriageModel.IsSuitableForMarriage))]
    public static class IsSuitableForMarriagePatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher matcher = new(instructions);

            matcher.MatchStartForward(CodeMatch.Calls(AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.Army))))
                .SetOperandAndAdvance(AccessTools.Method(typeof(IsSuitableForMarriagePatch), nameof(NullArmy)));

            if (matcher.Start().MatchStartForward(CodeMatch.Calls(AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.Spouse)))).IsValid)
            {
                matcher.SetInstruction(CodeInstruction.Call(typeof(IsSuitableForMarriagePatch), nameof(NullSpouse)));
            }

            return matcher.InstructionEnumeration();
        }

        public static Army NullArmy(MobileParty _) => null;
        public static Hero NullSpouse(Hero _) => null;
    }

    [HarmonyPatch(typeof(RomanceCampaignBehavior), "get_RomanceCourtshipAttemptCooldown")]
    public static class RomanceCourtshipAttemptCooldownPatch
    {
        public static bool Prefix(ref CampaignTime __result)
        {
            if (!SubModule.Instance.CampaignSettings.RemoveCooldown) return true;
            __result = CampaignTime.DaysFromNow(1);
            return false;
        }
    }

    [HarmonyPatch(typeof(MarriageAction), "HandleClanChangeAfterMarriageForHero")]
    public static class HandleClanChangeAfterMarriageForHeroPatch
    {
        public static bool Prefix(Hero hero, Clan clanAfterMarriage) => hero.Clan != clanAfterMarriage;
    }
}