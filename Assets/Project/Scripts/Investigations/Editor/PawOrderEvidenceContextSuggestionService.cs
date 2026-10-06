using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fabula.PawOrder;

namespace Fabula.PawOrder.Editor
{
    internal static class PawOrderEvidenceContextSuggestionService
    {
        #region Public API

        internal static List<PawOrderEvidenceSuggestion> BuildSuggestions(
            InteractionOutcomeData outcome,
            IReadOnlyList<CharacterData> suspects,
            PawOrderEvidenceContextDatabase contextDatabase)
        {
            List<PawOrderEvidenceSuggestion> suggestions = PawOrderEvidenceAuthoringUtility.BuildTextSuggestions(outcome, suspects);
            if (outcome == null || suspects == null || suspects.Count == 0 || contextDatabase == null)
            {
                return suggestions;
            }

            Dictionary<CharacterData, PawOrderEvidenceSuggestionBuilder> builders = CreateBuilders(suggestions);
            string responseText = outcome.ResponseText ?? string.Empty;
            AddLocationSuggestions(responseText, suspects, contextDatabase, builders);
            AddItemSuggestions(responseText, suspects, contextDatabase, builders);
            return builders.Values.Select(builder => builder.Build()).OrderBy(suggestion => PawOrderEvidenceAuthoringUtility.GetCharacterLabel(suggestion.Suspect), StringComparer.OrdinalIgnoreCase).ToList();
        }

        internal static List<PawOrderEvidenceContextMatch> FindContextMatches(InteractionOutcomeData outcome, PawOrderEvidenceContextDatabase contextDatabase)
        {
            List<PawOrderEvidenceContextMatch> matches = new List<PawOrderEvidenceContextMatch>();
            if (outcome == null || contextDatabase == null)
            {
                return matches;
            }

            string responseText = outcome.ResponseText ?? string.Empty;
            foreach (PawOrderEvidenceContextLocation location in contextDatabase.Locations)
            {
                if (ContainsAnyAlias(responseText, location.LocationName, location.Aliases, out string matchedAlias))
                {
                    matches.Add(new PawOrderEvidenceContextMatch("Location", location.LocationName, matchedAlias));
                }
            }

            foreach (PawOrderEvidenceContextItem item in contextDatabase.Items)
            {
                if (ContainsAnyAlias(responseText, item.ItemName, item.Aliases, out string matchedAlias))
                {
                    matches.Add(new PawOrderEvidenceContextMatch("Item", item.ItemName, matchedAlias));
                }
            }

            return matches.OrderBy(match => match.ContextType, StringComparer.OrdinalIgnoreCase).ThenBy(match => match.ContextName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        #endregion

        #region Internal Logic

        private static Dictionary<CharacterData, PawOrderEvidenceSuggestionBuilder> CreateBuilders(IEnumerable<PawOrderEvidenceSuggestion> seedSuggestions)
        {
            Dictionary<CharacterData, PawOrderEvidenceSuggestionBuilder> builders = new Dictionary<CharacterData, PawOrderEvidenceSuggestionBuilder>();
            foreach (PawOrderEvidenceSuggestion suggestion in seedSuggestions)
            {
                if (suggestion.Suspect == null)
                {
                    continue;
                }

                if (!builders.TryGetValue(suggestion.Suspect, out PawOrderEvidenceSuggestionBuilder builder))
                {
                    builder = new PawOrderEvidenceSuggestionBuilder(suggestion.Suspect);
                    builders[suggestion.Suspect] = builder;
                }

                builder.Apply(suggestion);
            }

            return builders;
        }

        private static void AddLocationSuggestions(
            string responseText,
            IReadOnlyList<CharacterData> suspects,
            PawOrderEvidenceContextDatabase contextDatabase,
            Dictionary<CharacterData, PawOrderEvidenceSuggestionBuilder> builders)
        {
            foreach (PawOrderEvidenceContextLocation location in contextDatabase.Locations)
            {
                if (!ContainsAnyAlias(responseText, location.LocationName, location.Aliases, out string matchedAlias))
                {
                    continue;
                }

                foreach (PawOrderEvidenceContextLocationLink link in contextDatabase.GetLocationLinks(location.LocationName))
                {
                    CharacterData suspect = FindSuspectByContextName(suspects, link.CharacterName);
                    if (suspect == null)
                    {
                        continue;
                    }

                    PawOrderEvidenceSuggestionBuilder builder = GetOrCreateBuilder(builders, suspect);
                    builder.OpportunityScore = Math.Max(builder.OpportunityScore, 1);
                    builder.AddReason("Location context: '" + matchedAlias + "' matched '" + link.LocationName + "'. " + link.CharacterName + " is linked through " + link.RelationshipType + " (" + link.Source + ").");
                }
            }
        }

        private static void AddItemSuggestions(
            string responseText,
            IReadOnlyList<CharacterData> suspects,
            PawOrderEvidenceContextDatabase contextDatabase,
            Dictionary<CharacterData, PawOrderEvidenceSuggestionBuilder> builders)
        {
            foreach (PawOrderEvidenceContextItem item in contextDatabase.Items)
            {
                if (!ContainsAnyAlias(responseText, item.ItemName, item.Aliases, out string matchedAlias))
                {
                    continue;
                }

                foreach (PawOrderEvidenceContextItemLink link in contextDatabase.GetItemLinks(item.ItemName))
                {
                    CharacterData suspect = FindSuspectByContextName(suspects, link.CharacterName);
                    if (suspect == null)
                    {
                        continue;
                    }

                    PawOrderEvidenceSuggestionBuilder builder = GetOrCreateBuilder(builders, suspect);
                    if (link.IsDirectContact)
                    {
                        builder.Score = Math.Max(builder.Score, 1);
                        builder.MeansScore = Math.Max(builder.MeansScore, 1);
                        builder.OpportunityScore = Math.Max(builder.OpportunityScore, 1);
                    }
                    else
                    {
                        builder.OpportunityScore = Math.Max(builder.OpportunityScore, 1);
                    }

                    string locationText = string.IsNullOrWhiteSpace(link.LocationName) ? string.Empty : " at '" + link.LocationName + "'";
                    builder.AddReason("Item context: '" + matchedAlias + "' matched '" + link.ItemName + "'. " + link.CharacterName + " is linked by " + link.RelationshipType + locationText + " (" + link.Source + ").");
                }
            }
        }

        private static PawOrderEvidenceSuggestionBuilder GetOrCreateBuilder(Dictionary<CharacterData, PawOrderEvidenceSuggestionBuilder> builders, CharacterData suspect)
        {
            if (!builders.TryGetValue(suspect, out PawOrderEvidenceSuggestionBuilder builder))
            {
                builder = new PawOrderEvidenceSuggestionBuilder(suspect);
                builders[suspect] = builder;
            }

            return builder;
        }

        private static CharacterData FindSuspectByContextName(IReadOnlyList<CharacterData> suspects, string contextCharacterName)
        {
            foreach (CharacterData suspect in suspects)
            {
                if (suspect == null)
                {
                    continue;
                }

                if (MatchesCharacterName(suspect, contextCharacterName))
                {
                    return suspect;
                }
            }

            return null;
        }

        private static bool MatchesCharacterName(CharacterData suspect, string contextCharacterName)
        {
            if (string.IsNullOrWhiteSpace(contextCharacterName))
            {
                return false;
            }

            return NamesMatch(suspect.name, contextCharacterName)
                || NamesMatch(suspect.DisplayName, contextCharacterName)
                || NamesMatch(suspect.CharacterId, contextCharacterName)
                || NamesMatch(GetFirstToken(suspect.name), contextCharacterName)
                || NamesMatch(GetFirstToken(suspect.DisplayName), contextCharacterName)
                || NamesMatch(GetFirstToken(suspect.CharacterId), contextCharacterName);
        }

        private static bool NamesMatch(string left, string right)
        {
            string leftKey = NormalizeComparable(left);
            string rightKey = NormalizeComparable(right);
            if (string.IsNullOrWhiteSpace(leftKey) || string.IsNullOrWhiteSpace(rightKey))
            {
                return false;
            }

            return string.Equals(leftKey, rightKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetFirstToken(leftKey), GetFirstToken(rightKey), StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsAnyAlias(string text, string canonicalName, IReadOnlyList<string> aliases, out string matchedAlias)
        {
            matchedAlias = string.Empty;
            if (ContainsToken(text, canonicalName))
            {
                matchedAlias = canonicalName;
                return true;
            }

            if (aliases == null)
            {
                return false;
            }

            foreach (string alias in aliases)
            {
                if (ContainsToken(text, alias))
                {
                    matchedAlias = alias;
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsToken(string text, string token)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(token) || token.Trim().Length < 3)
            {
                return false;
            }

            return CultureInfo.InvariantCulture.CompareInfo.IndexOf(
                text,
                token,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        }

        private static string NormalizeComparable(string value)
        {
            return (value ?? string.Empty).Trim().Replace("_", " ").Replace("-", " ");
        }

        private static string GetFirstToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string[] parts = value.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? value : parts[0];
        }

        #endregion
    }

    internal readonly struct PawOrderEvidenceContextMatch
    {
        #region Properties

        internal string ContextType { get; }
        internal string ContextName { get; }
        internal string MatchedAlias { get; }

        #endregion

        #region Constructors

        internal PawOrderEvidenceContextMatch(string contextType, string contextName, string matchedAlias)
        {
            ContextType = contextType;
            ContextName = contextName;
            MatchedAlias = matchedAlias;
        }

        #endregion
    }

    internal sealed class PawOrderEvidenceSuggestionBuilder
    {
        #region Fields

        private readonly List<string> reasons = new List<string>();

        #endregion

        #region Properties

        internal CharacterData Suspect { get; }
        internal int Score { get; set; }
        internal int MotivationScore { get; set; }
        internal int MeansScore { get; set; }
        internal int OpportunityScore { get; set; }

        #endregion

        #region Constructors

        internal PawOrderEvidenceSuggestionBuilder(CharacterData suspect)
        {
            Suspect = suspect;
        }

        #endregion

        #region Public API

        internal void Apply(PawOrderEvidenceSuggestion suggestion)
        {
            Score = Math.Max(Score, suggestion.Score);
            MotivationScore = Math.Max(MotivationScore, suggestion.MotivationScore);
            MeansScore = Math.Max(MeansScore, suggestion.MeansScore);
            OpportunityScore = Math.Max(OpportunityScore, suggestion.OpportunityScore);
            AddReason(suggestion.Reason);
        }

        internal void AddReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason) || reasons.Contains(reason))
            {
                return;
            }

            reasons.Add(reason);
        }

        internal PawOrderEvidenceSuggestion Build()
        {
            return new PawOrderEvidenceSuggestion(Suspect, Score, MotivationScore, MeansScore, OpportunityScore, string.Join(Environment.NewLine, reasons));
        }

        #endregion
    }
}
