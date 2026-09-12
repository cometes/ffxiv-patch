using System;
using System.Collections.Generic;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal enum TextPatchProfile
    {
        Full,
        Story,
        Custom
    }

    internal enum TextScope
    {
        Story = 0,
        BattleNpcNames = 1,
        Actions = 2,
        DutyNames = 3,
        ItemNames = 4,
        PlaceNames = 5,
        CommonPhrases = 6,
        Remainder = 7
    }

    internal enum TextScopeOutcome
    {
        Korean,
        Base
    }

    internal sealed class TextScopePolicy
    {
        private const int ScopeCount = 8;
        private readonly TextScopeOutcome[] _outcomes;

        private TextScopePolicy(TextPatchProfile profile, TextScopeOutcome[] outcomes)
        {
            if (outcomes == null || outcomes.Length != ScopeCount)
            {
                throw new ArgumentException("Exactly eight text scope outcomes are required.", "outcomes");
            }

            Profile = profile;
            _outcomes = (TextScopeOutcome[])outcomes.Clone();
        }

        public TextPatchProfile Profile { get; private set; }

        public bool MayUseKorean
        {
            get
            {
                for (int i = 0; i < _outcomes.Length; i++)
                {
                    if (_outcomes[i] == TextScopeOutcome.Korean)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static TextScopePolicy CreateFull()
        {
            TextScopeOutcome[] outcomes = new TextScopeOutcome[ScopeCount];
            for (int i = 0; i < outcomes.Length; i++)
            {
                outcomes[i] = TextScopeOutcome.Korean;
            }

            return new TextScopePolicy(TextPatchProfile.Full, outcomes);
        }

        public static TextScopePolicy CreateStory()
        {
            TextScopeOutcome[] outcomes = new TextScopeOutcome[ScopeCount];
            for (int i = 0; i < outcomes.Length; i++)
            {
                outcomes[i] = TextScopeOutcome.Base;
            }

            outcomes[(int)TextScope.Story] = TextScopeOutcome.Korean;
            return new TextScopePolicy(TextPatchProfile.Story, outcomes);
        }

        public static TextScopePolicy Parse(string profileValue, string outcomesValue)
        {
            TextPatchProfile profile = ParseProfile(profileValue);
            if (profile == TextPatchProfile.Full)
            {
                RequireNoCustomOutcomes(profile, outcomesValue);
                return CreateFull();
            }

            if (profile == TextPatchProfile.Story)
            {
                RequireNoCustomOutcomes(profile, outcomesValue);
                return CreateStory();
            }

            return ParseCustom(outcomesValue);
        }

        public TextScopeOutcome GetOutcome(TextScope scope)
        {
            int index = (int)scope;
            if (index < 0 || index >= _outcomes.Length)
            {
                throw new ArgumentOutOfRangeException("scope");
            }

            return _outcomes[index];
        }

        public TextSheetScopePolicy ForSheet(string sheetName)
        {
            return new TextSheetScopePolicy(this, sheetName);
        }

        public static string GetProfileId(TextPatchProfile profile)
        {
            switch (profile)
            {
                case TextPatchProfile.Full:
                    return "full";
                case TextPatchProfile.Story:
                    return "story";
                case TextPatchProfile.Custom:
                    return "custom";
                default:
                    throw new ArgumentOutOfRangeException("profile");
            }
        }

        public static string GetScopeId(TextScope scope)
        {
            switch (scope)
            {
                case TextScope.Story:
                    return "story";
                case TextScope.BattleNpcNames:
                    return "bnpc";
                case TextScope.Actions:
                    return "actions";
                case TextScope.DutyNames:
                    return "duty";
                case TextScope.ItemNames:
                    return "item";
                case TextScope.PlaceNames:
                    return "place";
                case TextScope.CommonPhrases:
                    return "common";
                case TextScope.Remainder:
                    return "remainder";
                default:
                    throw new ArgumentOutOfRangeException("scope");
            }
        }

        private static TextPatchProfile ParseProfile(string value)
        {
            string normalized = (value ?? "full").Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "full":
                    return TextPatchProfile.Full;
                case "story":
                    return TextPatchProfile.Story;
                case "custom":
                    return TextPatchProfile.Custom;
                default:
                    throw new ArgumentException("Unsupported text profile: " + value);
            }
        }

        private static void RequireNoCustomOutcomes(TextPatchProfile profile, string outcomesValue)
        {
            if (!string.IsNullOrWhiteSpace(outcomesValue))
            {
                throw new ArgumentException(
                    "--text-scope-outcomes can only be used with --text-profile custom, not " +
                    GetProfileId(profile) + ".");
            }
        }

        private static TextScopePolicy ParseCustom(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("--text-profile custom requires --text-scope-outcomes for all eight scopes.");
            }

            TextScopeOutcome[] outcomes = new TextScopeOutcome[ScopeCount];
            bool[] assigned = new bool[ScopeCount];
            string[] entries = value.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i].Trim();
                int separator = entry.IndexOf('=');
                if (separator <= 0 || separator == entry.Length - 1)
                {
                    throw new ArgumentException("Invalid text scope outcome: " + entry);
                }

                TextScope scope = ParseScope(entry.Substring(0, separator));
                int scopeIndex = (int)scope;
                if (assigned[scopeIndex])
                {
                    throw new ArgumentException("Duplicate text scope outcome: " + GetScopeId(scope));
                }

                outcomes[scopeIndex] = ParseOutcome(entry.Substring(separator + 1));
                assigned[scopeIndex] = true;
            }

            List<string> missing = new List<string>();
            for (int i = 0; i < assigned.Length; i++)
            {
                if (!assigned[i])
                {
                    missing.Add(GetScopeId((TextScope)i));
                }
            }

            if (missing.Count > 0)
            {
                throw new ArgumentException(
                    "--text-scope-outcomes is missing: " + string.Join(",", missing.ToArray()));
            }

            return new TextScopePolicy(TextPatchProfile.Custom, outcomes);
        }

        private static TextScope ParseScope(string value)
        {
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "story":
                case "quest":
                case "quests":
                    return TextScope.Story;
                case "bnpc":
                case "bnpcname":
                case "battle-npc":
                case "battle-npc-names":
                    return TextScope.BattleNpcNames;
                case "action":
                case "actions":
                case "skills":
                    return TextScope.Actions;
                case "duty":
                case "duties":
                case "dutynames":
                    return TextScope.DutyNames;
                case "item":
                case "items":
                case "itemnames":
                    return TextScope.ItemNames;
                case "place":
                case "places":
                case "placenames":
                    return TextScope.PlaceNames;
                case "common":
                case "commonphrases":
                case "common-phrases":
                    return TextScope.CommonPhrases;
                case "remainder":
                case "other":
                    return TextScope.Remainder;
                default:
                    throw new ArgumentException("Unsupported text scope: " + value);
            }
        }

        private static TextScopeOutcome ParseOutcome(string value)
        {
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "ko":
                case "korean":
                    return TextScopeOutcome.Korean;
                case "base":
                case "original":
                    return TextScopeOutcome.Base;
                default:
                    throw new ArgumentException("Unsupported text scope outcome: " + value);
            }
        }
    }

    internal sealed class TextSheetScopePolicy
    {
        private readonly TextScopePolicy _policy;
        private readonly SheetKind _sheetKind;

        public TextSheetScopePolicy(TextScopePolicy policy, string sheetName)
        {
            _policy = policy ?? TextScopePolicy.CreateFull();

            string normalized = NormalizeSheetName(sheetName);
            string leafName = GetLeafName(normalized);
            if (IsWholeStorySheet(normalized, leafName))
            {
                _sheetKind = SheetKind.WholeStory;
            }
            else if (string.Equals(leafName, "InstanceContentTextData", StringComparison.OrdinalIgnoreCase))
            {
                _sheetKind = SheetKind.InstanceContentTextData;
            }
            else if (string.Equals(leafName, "BNpcName", StringComparison.OrdinalIgnoreCase))
            {
                _sheetKind = SheetKind.BattleNpcNames;
            }
            else if (IsActionSheet(leafName))
            {
                _sheetKind = SheetKind.Actions;
            }
            else if (string.Equals(leafName, "ContentFinderCondition", StringComparison.OrdinalIgnoreCase))
            {
                _sheetKind = SheetKind.DutyNames;
            }
            else if (string.Equals(leafName, "Item", StringComparison.OrdinalIgnoreCase))
            {
                _sheetKind = SheetKind.ItemNames;
            }
            else if (string.Equals(leafName, "PlaceName", StringComparison.OrdinalIgnoreCase))
            {
                _sheetKind = SheetKind.PlaceNames;
            }
            else if (string.Equals(leafName, "Completion", StringComparison.OrdinalIgnoreCase))
            {
                _sheetKind = SheetKind.CommonPhrases;
            }
            else
            {
                _sheetKind = SheetKind.Remainder;
            }
        }

        public TextScope Classify(uint rowId, ushort columnOffset)
        {
            switch (_sheetKind)
            {
                case SheetKind.WholeStory:
                    return TextScope.Story;
                case SheetKind.InstanceContentTextData:
                    return rowId >= 1000 ? TextScope.Story : TextScope.Remainder;
                case SheetKind.BattleNpcNames:
                    return columnOffset == 0 ? TextScope.BattleNpcNames : TextScope.Remainder;
                case SheetKind.Actions:
                    return columnOffset == 0 ? TextScope.Actions : TextScope.Remainder;
                case SheetKind.DutyNames:
                    return columnOffset == 0 || columnOffset == 4
                        ? TextScope.DutyNames
                        : TextScope.Remainder;
                case SheetKind.ItemNames:
                    return columnOffset == 0 || columnOffset == 4 || columnOffset == 12
                        ? TextScope.ItemNames
                        : TextScope.Remainder;
                case SheetKind.PlaceNames:
                    return columnOffset == 0 || columnOffset == 4 || columnOffset == 8
                        ? TextScope.PlaceNames
                        : TextScope.Remainder;
                case SheetKind.CommonPhrases:
                    return columnOffset == 0 || columnOffset == 4 || columnOffset == 8
                        ? TextScope.CommonPhrases
                        : TextScope.Remainder;
                default:
                    return TextScope.Remainder;
            }
        }
        public bool MayUseKorean
        {
            get
            {
                switch (_sheetKind)
                {
                    case SheetKind.WholeStory:
                        return IsKorean(TextScope.Story);
                    case SheetKind.InstanceContentTextData:
                        return IsKorean(TextScope.Story) || IsKorean(TextScope.Remainder);
                    case SheetKind.BattleNpcNames:
                        return IsKorean(TextScope.BattleNpcNames) || IsKorean(TextScope.Remainder);
                    case SheetKind.Actions:
                        return IsKorean(TextScope.Actions) || IsKorean(TextScope.Remainder);
                    case SheetKind.DutyNames:
                        return IsKorean(TextScope.DutyNames) || IsKorean(TextScope.Remainder);
                    case SheetKind.ItemNames:
                        return IsKorean(TextScope.ItemNames) || IsKorean(TextScope.Remainder);
                    case SheetKind.PlaceNames:
                        return IsKorean(TextScope.PlaceNames) || IsKorean(TextScope.Remainder);
                    case SheetKind.CommonPhrases:
                        return IsKorean(TextScope.CommonPhrases) || IsKorean(TextScope.Remainder);
                    default:
                        return IsKorean(TextScope.Remainder);
                }
            }
        }

        private bool IsKorean(TextScope scope)
        {
            return _policy.GetOutcome(scope) == TextScopeOutcome.Korean;
        }


        public bool ShouldUseKorean(uint rowId, ushort columnOffset)
        {
            return _policy.GetOutcome(Classify(rowId, columnOffset)) == TextScopeOutcome.Korean;
        }

        private static string NormalizeSheetName(string value)
        {
            return (value ?? string.Empty).Trim().TrimStart('/', '\\').Replace('\\', '/');
        }

        private static bool IsWholeStorySheet(string sheetName, string leafName)
        {
            if (sheetName.StartsWith("quest/", StringComparison.OrdinalIgnoreCase) ||
                sheetName.StartsWith("cut_scene/", StringComparison.OrdinalIgnoreCase) ||
                sheetName.StartsWith("opening/", StringComparison.OrdinalIgnoreCase) ||
                sheetName.StartsWith("custom/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return leafName.IndexOf("Talk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   string.Equals(leafName, "Balloon", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(leafName, "NpcYell", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(leafName, "TopicSelect", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(leafName, "Quest", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(leafName, "CompleteJournal", StringComparison.OrdinalIgnoreCase) ||
                   leafName.StartsWith("QuestRedoChapterUI", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsActionSheet(string leafName)
        {
            return string.Equals(leafName, "Action", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(leafName, "BuddyAction", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(leafName, "CraftAction", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(leafName, "EventAction", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(leafName, "GeneralAction", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(leafName, "PetAction", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetLeafName(string sheetName)
        {
            int separator = sheetName.LastIndexOf('/');
            return separator < 0 ? sheetName : sheetName.Substring(separator + 1);
        }

        private enum SheetKind
        {
            Remainder,
            WholeStory,
            InstanceContentTextData,
            BattleNpcNames,
            Actions,
            DutyNames,
            ItemNames,
            PlaceNames,
            CommonPhrases
        }
    }
}
