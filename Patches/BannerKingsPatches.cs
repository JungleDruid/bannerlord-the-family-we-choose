using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BannerKings;
using BannerKings.Behaviours.Marriage;
using BannerKings.Managers.Institutions.Religions;
using BannerKings.Utils;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using TaleWorlds.Localization;

// ReSharper disable InconsistentNaming

namespace TheFamilyWeChoose.Patches;

public static class BannerKingsPatches
{
    public static bool PatchAll(Harmony harmony)
    {
        MethodInfo originalMethod = AccessTools.Method("BannerKings.UI.Marriages.MarriageContractProposalVM:RefreshValues");
        if (originalMethod is null) return false;
        SubModule.Instance.Logger.LogDebug($"Patching {originalMethod}");
        harmony.Patch(originalMethod,
            transpiler: AccessTools.Method(typeof(MarriageContractProposalVMRefreshValuesPatch), nameof(MarriageContractProposalVMRefreshValuesPatch.Transpiler)));

        originalMethod = FindOnSessionLaunchedFlirtationLineDelegate();
        SubModule.Instance.Logger.LogDebug($"Patching {originalMethod}");
        harmony.Patch(originalMethod,
            transpiler: AccessTools.Method(typeof(CampaignSystemPatches.PlayerCanOpenCourtshipOnConditionPatch),
                nameof(CampaignSystemPatches.PlayerCanOpenCourtshipOnConditionPatch.Transpiler)));

        originalMethod = AccessTools.Method("BannerKings.Models.Vanilla.BKMarriageModel:IsMarriageAdequate");
        SubModule.Instance.Logger.LogDebug($"Patching {originalMethod}");
        harmony.Patch(originalMethod,
            transpiler: AccessTools.Method(typeof(IsMarriageAdequatePatch), nameof(IsMarriageAdequatePatch.Transpiler)));

        originalMethod = PatchProcessor.GetOriginalInstructions(originalMethod).LastOrDefault(e => e.opcode == OpCodes.Ldftn)?.operand as MethodInfo;
        harmony.Patch(originalMethod,
            transpiler: AccessTools.Method(typeof(IsMarriageAdequatePatch), nameof(IsMarriageAdequatePatch.DelegateTranspiler)));

        originalMethod = AccessTools.Method("BannerKings.Behaviours.Marriage.BKMarriageBehavior:MakeSecondaryPartner");
        SubModule.Instance.Logger.LogDebug($"Patching {originalMethod}");
        harmony.Patch(originalMethod,
            prefix: AccessTools.Method(typeof(MakeSecondaryPartnerPatch), nameof(MakeSecondaryPartnerPatch.Prefix)));

        return true;
    }

    // ignore gender check on the proposal screen
    // [HarmonyPatch("BannerKings.UI.Marriages.MarriageContractProposalVM:RefreshValues")]
    private static class MarriageContractProposalVMRefreshValuesPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher matcher = new(instructions);

            MethodInfo isFemaleGetter = AccessTools.PropertyGetter(typeof(Hero), "IsFemale");
            MethodInfo proposedHeroGetter = AccessTools.PropertyGetter("BannerKings.UI.Marriages.MarriageContractProposalVM:ProposedHero");
            MethodInfo proposerHeroGetter = AccessTools.PropertyGetter("BannerKings.UI.Marriages.MarriageContractProposalVM:ProposerHero");

            for (int i = 0; i < 2; i++)
            {
                MethodInfo heroGetter = i == 0 ? proposedHeroGetter : proposerHeroGetter;
                SubModule.Instance.Logger.LogDebug($"Looping MarriageContractProposalVMRefreshValuesPatch {isFemaleGetter} {heroGetter}");
                matcher
                    .MatchEndForward(
                        CodeMatch.Calls(isFemaleGetter),
                        CodeMatch.LoadsLocal(),
                        CodeMatch.Calls(isFemaleGetter),
                        new CodeMatch(OpCodes.Ceq));

                SubModule.Instance.Logger.LogDebug(matcher.IsInvalid.ToString());
                if (matcher.IsInvalid) break;
                SubModule.Instance.Logger.LogDebug(matcher.Instruction.ToString());

                int endPos = matcher.Pos;

                matcher
                    .MatchStartBackwards(
                        CodeMatch.LoadsArgument(),
                        CodeMatch.Calls(heroGetter),
                        CodeMatch.Branches(),
                        CodeMatch.LoadsArgument(),
                        CodeMatch.Calls(heroGetter));

                SubModule.Instance.Logger.LogDebug(matcher.Instruction.ToString());
                int startPos = matcher.Pos;

                SubModule.Instance.Logger.LogDebug("Removing:");
                foreach (CodeInstruction instruction in matcher.Instructions(endPos - startPos + 2))
                    SubModule.Instance.Logger.LogTrace(instruction.ToString());
                matcher.RemoveInstructions(endPos - startPos + 2);

                SubModule.Instance.Logger.LogDebug("Applied MarriageContractProposalVMRefreshValuesPatch");
            }

            MethodInfo getConsorts = AccessTools.PropertyGetter("BannerKings.Managers.Institutions.Religions.Doctrines.Marriage.MarriageDoctrine:Consorts");
            CodeInstruction storeCanHaveCosorts = matcher.Start()
                .MatchStartForward(CodeMatch.Calls(getConsorts))
                .MatchEndBackwards(new CodeMatch(OpCodes.Ldc_I4_0), CodeMatch.StoresLocal())
                .Instruction;

            matcher
                .Advance(1)
                .Insert(
                    CodeInstruction.Call(typeof(MarriageContractProposalVMRefreshValuesPatch), nameof(AllowConsort)),
                    storeCanHaveCosorts
                );

            return matcher.Instructions();
        }

        private static bool AllowConsort() => SubModule.Instance.CampaignSettings.AllowConsort;
    }

    // editable consort penalty
    // [HarmonyPatch("BannerKings.Models.Vanilla.BKMarriageModel:IsMarriageAdequate")]
    private static class IsMarriageAdequatePatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher matcher = new(instructions);

            matcher
                .MatchStartForward(new CodeMatch(OpCodes.Ldstr, "{=!}Secondary spouse"))
                .MatchStartBackwards(CodeMatch.LoadsConstant())
                .SetInstruction(CodeInstruction.Call(typeof(IsMarriageAdequatePatch), nameof(ConsortPenalty)));

            return matcher.InstructionEnumeration();
        }
        
        public static IEnumerable<CodeInstruction> DelegateTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher matcher = new(instructions);

            MethodInfo isFemaleGetter = AccessTools.PropertyGetter(typeof(Hero), "IsFemale");

            // bool flag18 = this.proposer.IsFemale == this.secondHero.IsFemale;
            matcher.Start()
                .MatchStartForward(
                    CodeMatch.Calls(isFemaleGetter),
                    new CodeMatch(OpCodes.Ceq))
                .MatchStartBackwards(CodeMatch.IsLdarg())
                .Advance(-1)
                .MatchStartBackwards(CodeMatch.IsLdarg());

            var firstHeroInstructions = matcher.Instructions(2);
            var storeLocalOperand = (LocalBuilder)matcher.MatchStartForward(CodeMatch.StoresLocal()).Operand;
            matcher.MatchStartForward(CodeMatch.LoadsLocal())
                .Insert([
                    CodeInstruction.LoadLocal(storeLocalOperand.LocalIndex),
                    ..firstHeroInstructions,
                    CodeInstruction.Call(typeof(IsMarriageAdequatePatch), nameof(SameSexMarriageCheck)),
                    CodeInstruction.StoreLocal(storeLocalOperand.LocalIndex)
                ]);

            return matcher.InstructionEnumeration();
        }

        private static float ConsortPenalty() => SubModule.Instance.CampaignSettings.ConsortPenalty;

        private static bool SameSexMarriageCheck(bool result, Hero proposer)
        {
            if (!result || proposer != Hero.MainHero) return result;
            return SubModule.Instance.CampaignSettings.SexualOrientation == SexualOrientation.Heterosexual;
        }
    }

    // [HarmonyPatch("BannerKings.Behaviours.Marriage.BKMarriageBehavior:MakeSecondaryPartner")]
    private static class MakeSecondaryPartnerPatch
    {
        public static bool Prefix(Hero hero, Hero partner, BKMarriageBehavior __instance)
        {
            Religion religion = BannerKingsConfig.Instance.ReligionsManager.GetHeroReligion(hero);
            if (religion is not null) return true;
            __instance.GetHeroMarriage(hero).Partners.Add(partner);
            __instance.GetHeroMarriage(partner).PrimarySpouse = hero;
            InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=!}{HERO1} and {HERO2} are now united as secondary spouses!", null)
                .SetTextVariable("HERO1", hero.Name)
                .SetTextVariable("HERO2", partner.Name)
                .ToString(), Color.FromUint(TextHelper.COLOR_LIGHT_BLUE)));
            return false;
        }
    }

    private static MethodInfo FindOnSessionLaunchedFlirtationLineDelegate()
    {
        return AccessTools.TypeByName("BannerKings.Behaviours.Marriage.BKMarriageBehavior")?
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(method => PatchProcessor.GetCurrentInstructions(method)
                .Any(instruction => instruction.operand as string == "FLIRTATION_LINE"));
    }
}