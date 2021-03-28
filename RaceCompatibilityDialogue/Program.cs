using Mutagen.Bethesda;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RaceCompatibilityDialogue
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .SetTypicalOpen(GameRelease.SkyrimSE, "RaceCompatibilityDialogue.esp")
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .Run(args);
        }

        public static readonly Dictionary<IFormLinkGetter<IRaceGetter>, IFormLinkGetter<IKeywordGetter>> vanillaRaceToActorProxyKeywords = new()
        {
            { Skyrim.Race.ArgonianRace, RaceCompatibility.Keyword.ActorProxyArgonian },
            { Skyrim.Race.BretonRace, RaceCompatibility.Keyword.ActorProxyBreton },
            { Skyrim.Race.DarkElfRace, RaceCompatibility.Keyword.ActorProxyDarkElf },
            { Skyrim.Race.HighElfRace, RaceCompatibility.Keyword.ActorProxyHighElf },
            { Skyrim.Race.ImperialRace, RaceCompatibility.Keyword.ActorProxyImperial },
            { Skyrim.Race.KhajiitRace, RaceCompatibility.Keyword.ActorProxyKhajiit },
            { Skyrim.Race.NordRace, RaceCompatibility.Keyword.ActorProxyNord },
            { Skyrim.Race.OrcRace, RaceCompatibility.Keyword.ActorProxyOrc },
            { Skyrim.Race.RedguardRace, RaceCompatibility.Keyword.ActorProxyRedguard },
            { Skyrim.Race.WoodElfRace, RaceCompatibility.Keyword.ActorProxyWoodElf },
        };

        public static readonly HashSet<IFormLinkGetter<IKeywordGetter>> actorProxyKeywords = new(vanillaRaceToActorProxyKeywords.Values);

        public static readonly HashSet<Condition.Function> functionsOfInterest = new()
        {
            Condition.Function.GetIsRace,
            Condition.Function.GetPCIsRace
        };

        public static readonly Dictionary<IFormLinkGetter<IRaceGetter>, IFormLinkGetter<IRaceGetter>> vampireRaceToRace = new()
        {
            { Skyrim.Race.ArgonianRaceVampire, Skyrim.Race.ArgonianRace },
            { Skyrim.Race.BretonRaceVampire, Skyrim.Race.BretonRace },
            { Skyrim.Race.DarkElfRaceVampire, Skyrim.Race.DarkElfRace },
            { Skyrim.Race.HighElfRaceVampire, Skyrim.Race.HighElfRace },
            { Skyrim.Race.KhajiitRaceVampire, Skyrim.Race.KhajiitRace },
            { Skyrim.Race.NordRaceVampire, Skyrim.Race.NordRace },
            { Skyrim.Race.OrcRaceVampire, Skyrim.Race.OrcRace },
            { Skyrim.Race.RedguardRaceVampire, Skyrim.Race.RedguardRace },
            { Skyrim.Race.WoodElfRaceVampire, Skyrim.Race.WoodElfRace }
        };

        protected readonly LoadOrder<IModListing<ISkyrimModGetter>> LoadOrder;
        protected readonly ILinkCache<ISkyrimMod, ISkyrimModGetter> LinkCache;
        protected readonly ISkyrimMod PatchMod;

        public Program(LoadOrder<IModListing<ISkyrimModGetter>> loadOrder, ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, ISkyrimMod patchMod)
        {
            LoadOrder = loadOrder;
            LinkCache = linkCache;
            PatchMod = patchMod;
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var program = new Program(state.LoadOrder, state.LinkCache, state.PatchMod);

            program.RunPatch();
        }

        public void RunPatch()
        {
            int responseCounter = 0;
            var dialogueSet = new HashSet<IFormLink<IDialogTopicGetter>>();

            foreach (var item in LoadOrder.PriorityOrder.DialogResponses().WinningContextOverrides(LinkCache))
            {
                if (!IsVictim(item.Record)) continue;

                var response = item.GetOrAddAsOverride(PatchMod);

                responseCounter++;

                if (item.Parent?.Record is IDialogTopicGetter getter) dialogueSet.Add(getter.AsLink());

                AdjustResponses(response);
            }

            int dialogueCounter = dialogueSet.Count;

            Console.WriteLine($"Modified {responseCounter} responses to {dialogueCounter} dialogue topics.");
        }

        public static void AdjustResponses(IDialogResponses response)
        {
            var andList = GroupConditions(response.Conditions);

            AdjustConditions(andList);

            response.Conditions.Clear();
            foreach (var orList in andList)
                response.Conditions.AddRange(orList);
        }

        public static List<List<Condition>> GroupConditions(IList<Condition> conditions)
        {
            List<List<Condition>> andList = new();
            List<Condition> orList = new();

            foreach (var condition in conditions)
            {
                orList.Add(condition);
                if (!condition.Flags.HasFlag(Condition.Flag.OR)) {
                    andList.Add(orList);
                    orList = new();
                }
            }

            if (orList.Count > 0)
                andList.Add(orList);

            return andList;
        }

        public static void AdjustConditions(List<List<Condition>> andList)
        {
            Dictionary<IFormLinkGetter<IRaceGetter>, List<Condition>> isRace = new();
            Dictionary<IFormLinkGetter<IRaceGetter>, List<Condition>> isNotRace = new();

            foreach (var orList in andList)
            {
                List<Condition>? newConditions = null;

                isRace.Clear();
                isNotRace.Clear();

                foreach (var item in orList)
                {
                    if (item is not ConditionFloat condition) continue;
                    if (!IsBoolean(condition)) continue;
                    if (condition.Data is not FunctionConditionData data) continue;
                    if (!IsConditionOnPlayerRace(data)) continue;

                    var race = data.ParameterOneRecord.Cast<IRaceGetter>();

                    bool isVampireRace = vampireRaceToRace.TryGetValue(race, out var baseRace);
                    baseRace ??= race;

                    if (IsNot(condition))
                        isNotRace.Add(race);
                    else
                        isRace.Add(race);

                    var newCondition = condition.DeepCopy();
                    (newConditions ??= new()).Add(newCondition);

                    var newData = (FunctionConditionData)newCondition.Data;

                    newData.Function = Condition.Function.HasKeyword;
                    newData.ParameterOneRecord.SetTo(vanillaRaceToActorProxyKeywords[data.ParameterOneRecord.Cast<IRaceGetter>()]);

                    if (data.Function is Condition.Function.GetPCIsRace)
                    {
                        newData.RunOnType = Condition.RunOnType.Reference;
                        newData.Reference.SetTo(Constants.Player);
                    }
                }

                // TODO: Support is-x, is-vampire-x in addition to is-x-or-vampire-x (the existing keyword)
                //
                // current behaviour:
                //  * is race X => is actorProxyX or race X
                //
                // potential solution:
                //  * is race X or is race vampireX -> actorProxyX
                //  * is race X -> actorProxyNonVampireX (new)
                //  * is race vampireX -> actorProxyVampireX (new)
                //  
                // labels: enhancement

                if (newConditions != null)
                {
                    orList[^1].Flags |= Condition.Flag.OR;
                    foreach (var newCondition in newConditions)
                    {
                        newCondition.Flags |= Condition.Flag.OR;
                        orList.Add(newCondition);
                    }
                    orList[^1].Flags ^= Condition.Flag.OR;
                }
            }
        }

        public static bool IsNot(IConditionFloatGetter condition) => (condition.CompareOperator, condition.ComparisonValue) switch
        {
            (CompareOperator.EqualTo, 0) => true,
            (CompareOperator.LessThanOrEqualTo, 0) => true,
            (CompareOperator.NotEqualTo, 1) => true,
            (CompareOperator.LessThan, 1) => true,
            (CompareOperator.EqualTo, 1) => false,
            (CompareOperator.GreaterThanOrEqualTo, 1) => false,
            (CompareOperator.NotEqualTo, 0) => false,
            (CompareOperator.GreaterThan, 0) => false,
            (_, _) => throw new ArgumentException("not boolean",nameof(condition))
        };

        public static bool IsBoolean(IConditionFloatGetter condition) => Enum.IsDefined(condition.CompareOperator) && (condition.ComparisonValue) switch { 0 or 1 => true, _ => false };

        public static bool IsConditionOnPlayerRace(IFunctionConditionDataGetter x) => functionsOfInterest.Contains(x.Function)
                && vanillaRaceToActorProxyKeywords.ContainsKey(x.ParameterOneRecord.Cast<IRaceGetter>());

        public static bool IsConditionOnPlayerRaceProxyKeyword(IFunctionConditionDataGetter x) => x.Function == Condition.Function.HasKeyword
                && actorProxyKeywords.Contains(x.ParameterOneRecord);

        public static bool IsVictim(IDialogResponsesGetter x)
        {
            bool ok = false;
            foreach (var data in x.Conditions
                .OfType<IConditionFloatGetter>()
                .Where(x => IsBoolean(x))
                .Select(x => x.Data)
                .OfType<IFunctionConditionDataGetter>())
            {
                if (!ok && IsConditionOnPlayerRace(data))
                    ok = true;
                if (IsConditionOnPlayerRaceProxyKeyword(data))
                    return false;
            }
            return ok;
        }

    }
}
