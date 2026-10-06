using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Fabula.PawOrder.Editor
{
    internal sealed class PawOrderEvidenceImpactReport
    {
        #region Fields

        private readonly List<PawOrderEvidenceImpactRow> rows = new List<PawOrderEvidenceImpactRow>();
        private readonly List<PawOrderEvidenceImpactIssue> issues = new List<PawOrderEvidenceImpactIssue>();
        private readonly List<UnityEngine.Object> referencedAssets = new List<UnityEngine.Object>();

        #endregion

        #region Properties

        public IReadOnlyList<PawOrderEvidenceImpactRow> Rows => rows;
        public IReadOnlyList<PawOrderEvidenceImpactIssue> Issues => issues;
        public IReadOnlyList<UnityEngine.Object> ReferencedAssets => referencedAssets;
        public bool HasData => rows.Count > 0 || issues.Count > 0;

        #endregion

        #region Public API

        public void AddRow(PawOrderEvidenceImpactRow row)
        {
            if (row == null)
            {
                return;
            }

            rows.Add(row);
            AddAsset(row.Interaction);
            AddAsset(row.Outcome);
            AddAsset(row.Prompt);
            AddAsset(row.TargetCharacter);
            AddAsset(row.SuspectCharacter);
        }

        public void AddIssue(PawOrderEvidenceImpactSeverity severity, string category, string message, UnityEngine.Object context = null)
        {
            issues.Add(new PawOrderEvidenceImpactIssue(severity, category, message, context));
            AddAsset(context);
        }

        public string BuildMarkdown()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Paw & Order Evidence Impact Report");
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine("- Rows: " + rows.Count.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("- Issues: " + issues.Count.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("- Errors: " + CountIssues(PawOrderEvidenceImpactSeverity.Error));
            builder.AppendLine("- Warnings: " + CountIssues(PawOrderEvidenceImpactSeverity.Warning));
            builder.AppendLine("- Info: " + CountIssues(PawOrderEvidenceImpactSeverity.Info));
            builder.AppendLine();
            AppendSuspectSummary(builder);
            builder.AppendLine();
            AppendNpcSummary(builder);
            builder.AppendLine();
            AppendIssueSummary(builder);
            return builder.ToString();
        }

        public string BuildCsv()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("interactionId,outcomeId,questionId,questionText,targetNpc,suspect,motivationScore,meansScore,opportunityScore,score,scoreBand,responseText");
            foreach (PawOrderEvidenceImpactRow row in rows)
            {
                builder.AppendLine(string.Join(",",
                    Escape(row.InteractionId),
                    Escape(row.OutcomeId),
                    Escape(row.QuestionId),
                    Escape(row.QuestionText),
                    Escape(row.TargetNpcName),
                    Escape(row.SuspectName),
                    row.MotivationScore.ToString(CultureInfo.InvariantCulture),
                    row.MeansScore.ToString(CultureInfo.InvariantCulture),
                    row.OpportunityScore.ToString(CultureInfo.InvariantCulture),
                    row.Score.ToString(CultureInfo.InvariantCulture),
                    Escape(GetScoreBand(row.Score)),
                    Escape(row.ResponseText)));
            }

            return builder.ToString();
        }

        public static string GetScoreBand(int score)
        {
            if (score <= 15)
            {
                return "Irrelevant";
            }

            if (score <= 35)
            {
                return "Light clue";
            }

            if (score <= 60)
            {
                return "Moderate clue";
            }

            if (score <= 85)
            {
                return "Strong clue";
            }

            return "Central clue";
        }

        #endregion

        #region Internal Logic

        private void AddAsset(UnityEngine.Object asset)
        {
            if (asset == null || referencedAssets.Contains(asset))
            {
                return;
            }

            referencedAssets.Add(asset);
        }

        private string CountIssues(PawOrderEvidenceImpactSeverity severity)
        {
            return issues.Count(issue => issue.Severity == severity).ToString(CultureInfo.InvariantCulture);
        }

        private void AppendSuspectSummary(StringBuilder builder)
        {
            builder.AppendLine("## Summary by Suspect");
            if (rows.Count == 0)
            {
                builder.AppendLine("- No evidence rows found.");
                return;
            }

            foreach (IGrouping<string, PawOrderEvidenceImpactRow> suspectGroup in rows.GroupBy(row => row.SuspectName).OrderByDescending(group => group.Sum(row => row.Score)))
            {
                int totalScore = suspectGroup.Sum(row => row.Score);
                int motivationScore = suspectGroup.Sum(row => row.MotivationScore);
                int meansScore = suspectGroup.Sum(row => row.MeansScore);
                int opportunityScore = suspectGroup.Sum(row => row.OpportunityScore);
                builder.AppendLine("### " + suspectGroup.Key);
                builder.AppendLine("- Total score: " + totalScore);
                builder.AppendLine("- Axis totals: motivation=" + motivationScore + ", means=" + meansScore + ", opportunity=" + opportunityScore);
                builder.AppendLine("- Strongest outcomes: " + JoinTopOutcomes(suspectGroup));
            }
        }

        private void AppendNpcSummary(StringBuilder builder)
        {
            builder.AppendLine("## Summary by NPC");
            if (rows.Count == 0)
            {
                builder.AppendLine("- No NPC rows found.");
                return;
            }

            foreach (IGrouping<string, PawOrderEvidenceImpactRow> npcGroup in rows.GroupBy(row => row.TargetNpcName).OrderBy(group => group.Key))
            {
                builder.AppendLine("### " + npcGroup.Key);
                foreach (IGrouping<string, PawOrderEvidenceImpactRow> suspectGroup in npcGroup.GroupBy(row => row.SuspectName).OrderByDescending(group => group.Sum(row => row.Score)).Take(6))
                {
                    builder.AppendLine("- " + suspectGroup.Key + ": " + suspectGroup.Sum(row => row.Score));
                }
            }
        }

        private void AppendIssueSummary(StringBuilder builder)
        {
            builder.AppendLine("## Issues");
            if (issues.Count == 0)
            {
                builder.AppendLine("- No issues found.");
                return;
            }

            foreach (IGrouping<string, PawOrderEvidenceImpactIssue> categoryGroup in issues.GroupBy(issue => issue.Category).OrderByDescending(group => group.Count()))
            {
                builder.AppendLine("### " + categoryGroup.Key + " (" + categoryGroup.Count() + ")");
                foreach (PawOrderEvidenceImpactIssue issue in categoryGroup)
                {
                    builder.AppendLine("- [" + issue.Severity + "] " + issue.Message);
                }
            }
        }

        private static string JoinTopOutcomes(IEnumerable<PawOrderEvidenceImpactRow> rows)
        {
            string joined = string.Join(", ", rows.OrderByDescending(row => row.Score).Take(5).Select(row => row.OutcomeId + " [" + row.Score + "]"));
            return string.IsNullOrWhiteSpace(joined) ? "None" : joined;
        }

        private static string Escape(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        #endregion
    }

    internal sealed class PawOrderEvidenceImpactRow
    {
        #region Properties

        public CharacterInteractionData Interaction { get; set; }
        public InteractionOutcomeData Outcome { get; set; }
        public InvestigationPromptData Prompt { get; set; }
        public CharacterData TargetCharacter { get; set; }
        public CharacterData SuspectCharacter { get; set; }
        public string InteractionId { get; set; }
        public string OutcomeId { get; set; }
        public string QuestionId { get; set; }
        public string QuestionText { get; set; }
        public string TargetNpcName { get; set; }
        public string SuspectName { get; set; }
        public int MotivationScore { get; set; }
        public int MeansScore { get; set; }
        public int OpportunityScore { get; set; }
        public int Score { get; set; }
        public string ResponseText { get; set; }

        #endregion
    }

    internal sealed class PawOrderEvidenceImpactIssue
    {
        #region Constructors

        public PawOrderEvidenceImpactIssue(PawOrderEvidenceImpactSeverity severity, string category, string message, UnityEngine.Object context)
        {
            Severity = severity;
            Category = category;
            Message = message;
            Context = context;
        }

        #endregion

        #region Properties

        public PawOrderEvidenceImpactSeverity Severity { get; }
        public string Category { get; }
        public string Message { get; }
        public UnityEngine.Object Context { get; }

        #endregion
    }

    internal enum PawOrderEvidenceImpactSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }
}
