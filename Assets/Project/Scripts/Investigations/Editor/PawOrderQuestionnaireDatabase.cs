using System;
using System.Collections.Generic;
using System.Linq;

namespace Fabula.PawOrder.Editor
{
    internal sealed class PawOrderQuestionnaireDatabase
    {
        #region Fields

        private readonly List<PawOrderQuestionnaireQuestion> questions = new List<PawOrderQuestionnaireQuestion>();
        private readonly HashSet<string> npcNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Properties

        public IReadOnlyList<PawOrderQuestionnaireQuestion> Questions => questions;
        public IReadOnlyCollection<string> NpcNames => npcNames;

        #endregion

        #region Public API

        public static PawOrderQuestionnaireDatabase FromWorkbook(PawOrderExcelWorkbookReader workbook)
        {
            PawOrderQuestionnaireDatabase database = new PawOrderQuestionnaireDatabase();
            database.ImportQuestionMap(workbook);
            return database;
        }

        public bool ContainsQuestion(string questionId)
        {
            return questions.Any(question => string.Equals(question.QuestionId, questionId, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Internal Logic

        private void ImportQuestionMap(PawOrderExcelWorkbookReader workbook)
        {
            if (!workbook.TryGetSheet("Mapa Perguntas Final", out List<List<string>> rows))
            {
                return;
            }

            int headerRowIndex = FindQuestionHeaderRow(rows);
            if (headerRowIndex < 0)
            {
                return;
            }

            List<string> headerRow = rows[headerRowIndex];
            int idColumn = FindColumn(headerRow, "ID");
            int questionColumn = FindColumn(headerRow, "Pergunta");
            List<int> npcColumns = new List<int>();
            for (int columnIndex = 0; columnIndex < headerRow.Count; columnIndex++)
            {
                if (columnIndex == idColumn || columnIndex == questionColumn)
                {
                    continue;
                }

                string npcName = headerRow[columnIndex]?.Trim();
                if (string.IsNullOrWhiteSpace(npcName))
                {
                    continue;
                }

                npcColumns.Add(columnIndex);
                npcNames.Add(npcName);
            }

            for (int rowIndex = headerRowIndex + 1; rowIndex < rows.Count; rowIndex++)
            {
                List<string> row = rows[rowIndex];
                string rawQuestionId = ReadCell(row, idColumn);
                string questionText = ReadCell(row, questionColumn);
                string questionId = ExtractQuestionId(rawQuestionId);
                if (string.IsNullOrWhiteSpace(questionId) || string.IsNullOrWhiteSpace(questionText))
                {
                    continue;
                }

                PawOrderQuestionnaireQuestion question = new PawOrderQuestionnaireQuestion(questionId, rawQuestionId, questionText);
                foreach (int npcColumn in npcColumns)
                {
                    string npcName = headerRow[npcColumn]?.Trim();
                    string response = ReadCell(row, npcColumn);
                    if (string.IsNullOrWhiteSpace(npcName) || string.IsNullOrWhiteSpace(response))
                    {
                        continue;
                    }

                    question.AddResponse(npcName, response);
                }

                questions.Add(question);
            }
        }

        private static int FindQuestionHeaderRow(IReadOnlyList<List<string>> rows)
        {
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                if (FindColumn(rows[rowIndex], "ID") >= 0 && FindColumn(rows[rowIndex], "Pergunta") >= 0)
                {
                    return rowIndex;
                }
            }

            return -1;
        }

        private static int FindColumn(IReadOnlyList<string> row, string expectedValue)
        {
            for (int columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                if (string.Equals(row[columnIndex]?.Trim(), expectedValue, StringComparison.OrdinalIgnoreCase))
                {
                    return columnIndex;
                }
            }

            return -1;
        }

        private static string ReadCell(IReadOnlyList<string> row, int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= row.Count)
            {
                return string.Empty;
            }

            return row[columnIndex]?.Trim() ?? string.Empty;
        }

        private static string ExtractQuestionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            int spaceIndex = value.IndexOf(' ');
            if (spaceIndex <= 0)
            {
                return value.Trim();
            }

            return value.Substring(0, spaceIndex).Trim();
        }

        #endregion
    }

    internal sealed class PawOrderQuestionnaireQuestion
    {
        #region Fields

        private readonly Dictionary<string, string> responsesByNpc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Constructors

        public PawOrderQuestionnaireQuestion(string questionId, string rawQuestionId, string questionText)
        {
            QuestionId = questionId;
            RawQuestionId = rawQuestionId;
            QuestionText = questionText;
        }

        #endregion

        #region Properties

        public string QuestionId { get; }
        public string RawQuestionId { get; }
        public string QuestionText { get; }
        public IReadOnlyDictionary<string, string> ResponsesByNpc => responsesByNpc;

        #endregion

        #region Public API

        public void AddResponse(string npcName, string response)
        {
            responsesByNpc[npcName] = response;
        }

        #endregion
    }
}
