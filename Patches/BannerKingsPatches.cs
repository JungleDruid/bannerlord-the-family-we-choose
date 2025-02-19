using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BannerKings;
using BannerKings.Behaviours.Marriage;
using BannerKings.Managers.Institutions.Religions;
using BannerKings.Models.Vanilla;
using BannerKings.UI.Marriages;
using BannerKings.Utils;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

// ReSharper disable InconsistentNaming

namespace TheFamilyWeChoose.Patches;

public static class BannerKingsPatches
{
    public static bool PatchAll(Harmony harmony)
    {
        MethodInfo originalMethod = AccessTools.Method(typeof(MarriageContractProposalVM), nameof(MarriageContractProposalVM.RefreshValues));
        if (originalMethod is null) return false;
        SubModule.Instance.Logger.LogDebug($"Patching {originalMethod}");
        harmony.Patch(originalMethod,
            transpiler: AccessTools.Method(typeof(MarriageContractProposalVMRefreshValuesPatch), nameof(MarriageContractProposalVMRefreshValuesPatch.Transpiler)));

        originalMethod = FindOnSessionLaunchedFlirtationLineDelegate();
        SubModule.Instance.Logger.LogDebug($"Patching {originalMethod}");
        harmony.Patch(originalMethod,
            transpiler: AccessTools.Method(typeof(CampaignSystemPatches.PlayerCanOpenCourtshipOnConditionPatch),
                nameof(CampaignSystemPatches.PlayerCanOpenCourtshipOnConditionPatch.Transpiler)));

        originalMethod = AccessTools.Method(typeof(BKMarriageModel), nameof(BKMarriageModel.IsMarriageAdequate));
        SubModule.Instance.Logger.LogDebug($"Patching {originalMethod}");
        harmony.Patch(originalMethod,
            transpiler: AccessTools.Method(typeof(IsMarriageAdequatePatch), nameof(IsMarriageAdequatePatch.Transpiler)));

        originalMethod = PatchProcessor.GetOriginalInstructions(originalMethod).LastOrDefault(e => e.opcode == OpCodes.Ldftn)?.operand as MethodInfo;
        harmony.Patch(originalMethod,
            transpiler: AccessTools.Method(typeof(IsMarriageAdequatePatch), nameof(IsMarriageAdequatePatch.DelegateTranspiler)));

        originalMethod = AccessTools.Method(typeof(BKMarriageBehavior), nameof(BKMarriageBehavior.MakeSecondaryPartner));
        SubModule.Instance.Logger.LogDebug($"Patching {originalMethod}");
        harmony.Patch(originalMethod,
            prefix: AccessTools.Method(typeof(MakeSecondaryPartnerPatch), nameof(MakeSecondaryPartnerPatch.Prefix)));

        originalMethod = AccessTools.Method(typeof(BKMarriageModel), nameof(BKMarriageModel.IsSuitableForMarriage));
        SubModule.Instance.Logger.LogDebug($"Patching {originalMethod}");
        harmony.Patch(originalMethod,
            transpiler: AccessTools.Method(typeof(CampaignSystemPatches.IsSuitableForMarriagePatch), nameof(CampaignSystemPatches.IsSuitableForMarriagePatch.Transpiler)));

        originalMethod = AccessTools.Method(typeof(BKMarriageBehavior), "IsPotentialSpouseBK");
        SubModule.Instance.Logger.LogDebug($"Patching {originalMethod}");
        harmony.Patch(originalMethod,
            transpiler: AccessTools.Method(typeof(IsPotentialSpouseBKPatch), nameof(IsPotentialSpouseBKPatch.Transpiler)));

        originalMethod = AccessTools.Method(typeof(BKMarriageBehavior), nameof(BKMarriageBehavior.IsCoupleMatchedByFamily));
        SubModule.Instance.Logger.LogDebug($"Patching {originalMethod}");
        harmony.Patch(originalMethod,
            prefix: AccessTools.Method(typeof(IsCoupleMatchedByFamilyPatch), nameof(IsCoupleMatchedByFamilyPatch.Prefix)));

        foreach (MethodInfo method in FindOnSessionLaunchedOfferMyHandDelegates())
        {
            SubModule.Instance.Logger.LogDebug($"Patching {method}");
            harmony.Patch(method, postfix: AccessTools.Method(typeof(BannerKingsPatches), nameof(OfferMyHandPostfix)));
        }

        originalMethod = AccessTools.Method(typeof(MarriageAction), nameof(MarriageAction.Apply));
        SubModule.Instance.Logger.LogDebug($"Patching {originalMethod}");
        harmony.Patch(originalMethod,
            prefix: AccessTools.Method(typeof(MarriageActionApplyPatch), nameof(MarriageActionApplyPatch.Prefix)));

        SubModule.Instance.Logger.LogInformation("All patches of Banner Kings have been applied successfully.");
        return true;
    }

    public static void AddDialogues(CampaignGameStarter starter)
    {
        starter.AddPlayerLine("bk_marriage_offered_clan_member",
            "lord_start_courtship_response_player_offer",
            "lord_start_courtship_response_2",
            "{=cKtJBdPD}I wish to offer my hand in marriage.",
            () =>
            {
                if (Hero.OneToOneConversationHero == null || Hero.OneToOneConversationHero.Clan == null)
                {
                    return false;
                }

                return Hero.OneToOneConversationHero.Clan == Clan.PlayerClan && Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero) == Romance.RomanceLevelEnum.Untested;
            },
            conversation_player_opens_courtship_on_consequence,
            200);

        starter.AddPlayerLine("bk_marriage_offered_clan_member_already_flirted",
            "lord_talk_speak_diplomacy_2",
            "lord_start_courtship_response_2",
            "{=cKtJBdPD}I wish to offer my hand in marriage.",
            () =>
            {
                if (Hero.OneToOneConversationHero == null || Hero.OneToOneConversationHero.Clan == null)
                {
                    return false;
                }

                var flirtedWith = AccessTools.FieldRefAccess<BKMarriageBehavior, List<Hero>>(Campaign.Current.GetCampaignBehavior<BKMarriageBehavior>(), "flirtedWith");

                return Hero.OneToOneConversationHero.Clan == Clan.PlayerClan &&
                       Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero) == Romance.RomanceLevelEnum.Untested &&
                       flirtedWith.Contains(Hero.OneToOneConversationHero);
            },
            conversation_player_opens_courtship_on_consequence,
            200);

        starter.AddDialogLine("persuasion_leave_faction_npc_result_success_clan_member", "lord_conclude_courtship_stage_2", "close_window",
            "{=TFWCClanAcceptProposal}I am forever yours.",
            () => Hero.MainHero.Clan == Hero.OneToOneConversationHero.Clan,
            () =>
            {
                MarriageAction.Apply(Hero.MainHero, Hero.OneToOneConversationHero);

                if (PlayerEncounter.Current != null)
                {
                    PlayerEncounter.LeaveEncounter = true;
                }
            }, 150);

        return;

        static void conversation_player_opens_courtship_on_consequence()
        {
            if (Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero) == Romance.RomanceLevelEnum.FailedInCompatibility ||
                Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero) == Romance.RomanceLevelEnum.FailedInPracticalities)
                return;
            ChangeRomanticStateAction.Apply(Hero.MainHero, Hero.OneToOneConversationHero, Romance.RomanceLevelEnum.CourtshipStarted);
        }
    }

    // [HarmonyPatch(typeof(MarriageAction), nameof(MarriageAction.Apply))]
    private static class MarriageActionApplyPatch
    {
        private static readonly MethodInfo ApplyInternal = AccessTools.Method(typeof(MarriageAction), "ApplyInternal");

        public static bool Prefix(Hero firstHero, Hero secondHero, bool showNotification)
        {
            if (firstHero != Hero.MainHero || firstHero.Spouse == null) return true;

            Religion religion = BannerKingsConfig.Instance.ReligionsManager.GetHeroReligion(firstHero);
            bool allowConsort = SubModule.Instance.CampaignSettings.AllowConsort;

            if (religion != null)
                allowConsort = religion.Faith.MarriageDoctrine.Consorts > Campaign.Current.GetCampaignBehavior<BKMarriageBehavior>().GetHeroPartners(firstHero).Count;

            if (allowConsort)
            {
                InformationManager.ShowInquiry(new InquiryData(
                    new TextObject("{=TFWCSpouseChoice}Spouse Choice").ToString(),
                    new TextObject("{=TFWCSpouseOrConsortContent}Will {NEW_SPOUSE} be your primary spouse or secondary spouse? {CURRENT_SPOUSE} will become secondary if {NEW_SPOUSE} becomes primary.",
                            new Dictionary<string, object>
                            {
                                ["CURRENT_SPOUSE"] = firstHero.Spouse.Name,
                                ["NEW_SPOUSE"] = secondHero.Name
                            })
                        .ToString(),
                    true, true,
                    new TextObject("{=TFWCPrimary}Primary").ToString(),
                    new TextObject("{=TFWCSecondary}Secondary").ToString(),
                    () =>
                    {
                        Hero spouse = firstHero.Spouse;
                        ApplyInternal.Invoke(null, [firstHero, secondHero, showNotification]);
                        Campaign.Current.GetCampaignBehavior<BKMarriageBehavior>().MakeSecondaryPartner(firstHero, spouse);
                    },
                    () => Campaign.Current.GetCampaignBehavior<BKMarriageBehavior>().MakeSecondaryPartner(firstHero, secondHero)
                ), true, true);
            }
            else
            {
                InformationManager.ShowInquiry(new InquiryData(
                    new TextObject("{=TFWCSpouseChoice}Spouse Choice").ToString(),
                    new TextObject("{=TFWCReplaceSpouseContent}Replace {CURRENT_SPOUSE} with {NEW_SPOUSE} as your spouse?",
                        new Dictionary<string, object>
                        {
                            ["CURRENT_SPOUSE"] = firstHero.Spouse.Name,
                            ["NEW_SPOUSE"] = secondHero.Name
                        }).ToString(),
                    true, true,
                    GameTexts.FindText("str_yes").ToString(),
                    GameTexts.FindText("str_no").ToString(),
                    () => ApplyInternal.Invoke(null, [firstHero, secondHero, showNotification]),
                    () => ChangeRomanticStateAction.Apply(firstHero, secondHero, Romance.RomanceLevelEnum.FailedInCompatibility)
                ), true, true);
            }

            return false;
        }
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
            InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=!}{HERO1} and {HERO2} are now united as secondary spouses!")
                .SetTextVariable("HERO1", hero.Name)
                .SetTextVariable("HERO2", partner.Name)
                .ToString(), Color.FromUint(TextHelper.COLOR_LIGHT_BLUE)));
            return false;
        }
    }

    // [HarmonyPatch(typeof(BKMarriageBehavior), "IsPotentialSpouseBK")]
    private static class IsPotentialSpouseBKPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher matcher = new(instructions);

            matcher.MatchStartForward(CodeMatch.Calls(AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.Spouse))))
                .SetInstruction(CodeInstruction.Call(typeof(CampaignSystemPatches.IsSuitableForMarriagePatch),
                    nameof(CampaignSystemPatches.IsSuitableForMarriagePatch.NullSpouse)));

            return matcher.InstructionEnumeration();
        }
    }

    // [HarmonyPatch(typeof(BKMarriageBehavior), nameof(BKMarriageBehavior.IsCoupleMatchedByFamily))]
    private static class IsCoupleMatchedByFamilyPatch
    {
        public static bool Prefix(Hero proposer, Hero proposed, ref bool __result)
        {
            if (proposer.Clan != proposed.Clan || proposer.Clan != Clan.PlayerClan) return true;
            __result = true;
            return false;
        }
    }

    private static void OfferMyHandPostfix(ref bool __result)
    {
        if (!__result) return;
        __result = Hero.MainHero.Clan != Hero.OneToOneConversationHero.Clan;
    }

    private static MethodInfo FindOnSessionLaunchedFlirtationLineDelegate()
    {
        return typeof(BKMarriageBehavior)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(method => PatchProcessor.GetCurrentInstructions(method)
                .Any(instruction => instruction.operand as string == "FLIRTATION_LINE"));
    }

    private static IEnumerable<MethodInfo> FindOnSessionLaunchedOfferMyHandDelegates()
    {
        MethodInfo method = AccessTools.Method(typeof(BKMarriageBehavior), "OnSessionLaunched");
        var instructions = PatchProcessor.GetCurrentInstructions(method);
        for (int index = 0; index < instructions.Count; index++)
        {
            CodeInstruction instruction = instructions[index];
            if (instruction.operand as string != "{=cKtJBdPD}I wish to offer my hand in marriage.") continue;
            yield return (MethodInfo)instructions[index + 2].operand;
        }
    }
}