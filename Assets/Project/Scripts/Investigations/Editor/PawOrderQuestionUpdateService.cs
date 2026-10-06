using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Fabula.PawOrder.Editor
{
    internal sealed class PawOrderQuestionUpdateService
    {
        #region Fields

        private static readonly Regex ItemReferenceRegex = new Regex(@"\[(I\d+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SuperscriptUnlockRegex = new Regex(@"\(([0-9\u2070\u00B9\u00B2\u00B3\u2074\u2075\u2076\u2077\u2078\u2079]+)\)", RegexOptions.Compiled);
        private static readonly Regex QuestionCodeRegex = new Regex(@"^\s*Q([0-9]+)(?:\b|\s|[_\-\(]).*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ParenthesizedQuestionCodeRegex = new Regex(@"^\s*\(([0-9\u2070\u00B9\u00B2\u00B3\u2074\u2075\u2076\u2077\u2078\u2079]+)\)", RegexOptions.Compiled);

        private readonly string rootFolder;
        private readonly string questionsFolder;
        private readonly string outcomesFolder;
        private readonly string interactionsFolder;
        private readonly string casesFolder;
        private readonly string referenceLocationsFolder;

        #endregion

        #region Constructors

        public PawOrderQuestionUpdateService(string rootFolder)
        {
            this.rootFolder = NormalizeAssetFolder(rootFolder);
            questionsFolder = this.rootFolder + "/Questions";
            outcomesFolder = this.rootFolder + "/Outcomes";
            interactionsFolder = this.rootFolder + "/Interactions";
            casesFolder = this.rootFolder + "/Cases";
            referenceLocationsFolder = this.rootFolder + "/References/Locations";
        }

        #endregion

        #region Public API

        public PawOrderQuestionUpdateReport PreviewUpdate(string workbookPath)
        {
            return ProcessUpdate(workbookPath, false);
        }

        public PawOrderQuestionUpdateReport ApplyUpdate(string workbookPath)
        {
            return ProcessUpdate(workbookPath, true);
        }

        #endregion

        #region Internal Logic

        private PawOrderQuestionUpdateReport ProcessUpdate(string workbookPath, bool applyChanges)
        {
            PawOrderQuestionUpdateReport report = new PawOrderQuestionUpdateReport();
            if (!ValidateInput(workbookPath, report))
            {
                return report;
            }

            PawOrderExcelWorkbookReader workbook = PawOrderExcelWorkbookReader.Load(workbookPath);
            Dictionary<string, QuestionPromptData> questionsById = LoadQuestionsById();
            Dictionary<string, QuestionPromptData> questionsByOriginalImporterKey = LoadQuestionsByOriginalImporterKey();
            Dictionary<string, CharacterData> charactersByKey = LoadCharactersByKey();
            Dictionary<string, ItemPromptData> itemsByKey = LoadItemsByKey();
            Dictionary<string, LocationData> locationsByKey = LoadLocationsByKey();
            List<PawOrderQuestionnaireQuestion> questions = LoadCompatibleQuestions(workbook, charactersByKey, itemsByKey, locationsByKey, report);
            if (questions.Count == 0)
            {
                report.AddWarning("No valid questions were found in any compatible workbook sheet.");
                return report;
            }

            if (applyChanges)
            {
                EnsureDirectory(questionsFolder);
                EnsureDirectory(outcomesFolder);
                EnsureDirectory(interactionsFolder);
                EnsureDirectory(casesFolder);
            }

            List<QuestionPromptData> questionsCreatedOrFound = new List<QuestionPromptData>();

            foreach (PawOrderQuestionnaireQuestion question in questions)
            {
                QuestionPromptData questionAsset = GetOrCreateQuestion(question, questionsById, questionsByOriginalImporterKey, report, applyChanges);
                if (questionAsset != null)
                {
                    questionsCreatedOrFound.Add(questionAsset);
                }
            }

            foreach (PawOrderQuestionnaireQuestion question in questions)
            {
                string canonicalQuestionId = BuildOriginalImporterQuestionId(question);
                if (!questionsById.TryGetValue(canonicalQuestionId, out QuestionPromptData questionAsset))
                {
                    continue;
                }

                foreach (KeyValuePair<string, string> responseByNpc in question.ResponsesByNpc)
                {
                    ProcessResponse(question, responseByNpc, questionAsset, questionsById, charactersByKey, itemsByKey, locationsByKey, report, applyChanges);
                }
            }

            if (applyChanges)
            {
                MergeQuestionsIntoCase(questionsCreatedOrFound.Distinct().ToList());
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            report.AddMessage(BuildCompletionMessage(applyChanges));
            return report;
        }

        private static List<PawOrderQuestionnaireQuestion> LoadCompatibleQuestions(
            PawOrderExcelWorkbookReader workbook,
            IReadOnlyDictionary<string, CharacterData> charactersByKey,
            IReadOnlyDictionary<string, ItemPromptData> itemsByKey,
            IReadOnlyDictionary<string, LocationData> locationsByKey,
            PawOrderQuestionUpdateReport report)
        {
            List<PawOrderQuestionnaireQuestion> questions = new List<PawOrderQuestionnaireQuestion>();
            HashSet<string> importedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, List<List<string>>> sheetByName in workbook.SheetsByName)
            {
                if (!TryImportQuestionSheet(sheetByName.Key, sheetByName.Value, questions, importedKeys, charactersByKey, itemsByKey, locationsByKey, report, out int importedCount, out int responseColumnCount))
                {
                    continue;
                }

                report.AddMessage("Imported " + importedCount + " question rows from sheet: " + sheetByName.Key + " using " + responseColumnCount + " response columns.");
            }

            return questions;
        }

        private static bool TryImportQuestionSheet(
            string sheetName,
            IReadOnlyList<List<string>> rows,
            IList<PawOrderQuestionnaireQuestion> questions,
            ISet<string> importedKeys,
            IReadOnlyDictionary<string, CharacterData> charactersByKey,
            IReadOnlyDictionary<string, ItemPromptData> itemsByKey,
            IReadOnlyDictionary<string, LocationData> locationsByKey,
            PawOrderQuestionUpdateReport report,
            out int importedCount,
            out int responseColumnCount)
        {
            importedCount = 0;
            responseColumnCount = 0;
            int headerRowIndex = FindQuestionHeaderRow(rows);
            if (headerRowIndex < 0)
            {
                return false;
            }

            List<string> headerRow = rows[headerRowIndex];
            int idColumn = FindColumn(headerRow, "ID");
            int questionColumn = FindColumn(headerRow, "Pergunta");
            List<ResponseColumnDefinition> responseColumns = FindResponseColumns(headerRow, idColumn, questionColumn, charactersByKey, itemsByKey, locationsByKey, report, sheetName);
            responseColumnCount = responseColumns.Count;

            if (responseColumns.Count == 0)
            {
                report.AddWarning("No character response columns were found in sheet: " + sheetName);
            }

            for (int rowIndex = headerRowIndex + 1; rowIndex < rows.Count; rowIndex++)
            {
                List<string> row = rows[rowIndex];
                string rawQuestionId = ReadCell(row, idColumn);
                string questionText = ReadCell(row, questionColumn);
                string questionId = ExtractQuestionId(rawQuestionId, sheetName);
                if (string.IsNullOrWhiteSpace(questionId) || string.IsNullOrWhiteSpace(questionText))
                {
                    continue;
                }

                string importKey = NormalizeLookupKey(sheetName + "|" + questionId + "|" + questionText);
                if (!importedKeys.Add(importKey))
                {
                    continue;
                }

                PawOrderQuestionnaireQuestion question = new PawOrderQuestionnaireQuestion(questionId, rawQuestionId, questionText);
                foreach (ResponseColumnDefinition responseColumn in responseColumns)
                {
                    string response = ReadCell(row, responseColumn.ColumnIndex);
                    if (string.IsNullOrWhiteSpace(response))
                    {
                        continue;
                    }

                    question.AddResponse(responseColumn.CharacterName, response);
                }

                questions.Add(question);
                importedCount++;
            }

            return true;
        }

        private static List<ResponseColumnDefinition> FindResponseColumns(
            IReadOnlyList<string> headerRow,
            int idColumn,
            int questionColumn,
            IReadOnlyDictionary<string, CharacterData> charactersByKey,
            IReadOnlyDictionary<string, ItemPromptData> itemsByKey,
            IReadOnlyDictionary<string, LocationData> locationsByKey,
            PawOrderQuestionUpdateReport report,
            string sheetName)
        {
            List<ResponseColumnDefinition> responseColumns = new List<ResponseColumnDefinition>();
            HashSet<string> mappedCharacters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int columnIndex = 0; columnIndex < headerRow.Count; columnIndex++)
            {
                if (columnIndex == idColumn || columnIndex == questionColumn)
                {
                    continue;
                }

                string header = headerRow[columnIndex]?.Trim();
                if (string.IsNullOrWhiteSpace(header) || IsKnownNonCharacterResponseColumn(header, itemsByKey, locationsByKey))
                {
                    continue;
                }

                if (!TryResolveCharacter(header, charactersByKey, out CharacterData character))
                {
                    continue;
                }

                string characterName = GetCharacterDisplayName(character);
                if (string.IsNullOrWhiteSpace(characterName))
                {
                    continue;
                }

                string columnKey = characterName + "|" + columnIndex;
                if (!mappedCharacters.Add(columnKey))
                {
                    continue;
                }

                responseColumns.Add(new ResponseColumnDefinition(columnIndex, characterName));
            }

            if (responseColumns.Count == 0)
            {
                ReportUnresolvedPotentialResponseColumns(headerRow, idColumn, questionColumn, itemsByKey, locationsByKey, report, sheetName);
            }

            return responseColumns;
        }

        private static void ReportUnresolvedPotentialResponseColumns(
            IReadOnlyList<string> headerRow,
            int idColumn,
            int questionColumn,
            IReadOnlyDictionary<string, ItemPromptData> itemsByKey,
            IReadOnlyDictionary<string, LocationData> locationsByKey,
            PawOrderQuestionUpdateReport report,
            string sheetName)
        {
            foreach (string header in headerRow.Where((_, index) => index != idColumn && index != questionColumn))
            {
                string trimmedHeader = header?.Trim();
                if (string.IsNullOrWhiteSpace(trimmedHeader) || IsKnownNonCharacterResponseColumn(trimmedHeader, itemsByKey, locationsByKey))
                {
                    continue;
                }

                report.AddWarning("Ignored unresolved response column in sheet " + sheetName + ": " + trimmedHeader);
            }
        }

        private static int FindQuestionHeaderRow(IReadOnlyList<List<string>> rows)
        {
            if (rows == null)
            {
                return -1;
            }

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
            if (row == null)
            {
                return -1;
            }

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
            if (row == null || columnIndex < 0 || columnIndex >= row.Count)
            {
                return string.Empty;
            }

            return row[columnIndex]?.Trim() ?? string.Empty;
        }

        private static string ExtractQuestionId(string value, string sheetName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.IsNullOrWhiteSpace(sheetName) ? string.Empty : sheetName.Trim();
            }

            int spaceIndex = value.IndexOf(' ');
            if (spaceIndex <= 0)
            {
                return value.Trim();
            }

            return value.Substring(0, spaceIndex).Trim();
        }

        private bool ValidateInput(string workbookPath, PawOrderQuestionUpdateReport report)
        {
            if (string.IsNullOrWhiteSpace(workbookPath))
            {
                report.AddWarning("Workbook path is empty.");
                return false;
            }

            if (!File.Exists(workbookPath))
            {
                report.AddWarning("Workbook file was not found: " + workbookPath);
                return false;
            }

            if (string.IsNullOrWhiteSpace(rootFolder) || !rootFolder.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                report.AddWarning("Root folder must be an Assets path.");
                return false;
            }

            return true;
        }

        private QuestionPromptData GetOrCreateQuestion(
            PawOrderQuestionnaireQuestion question,
            Dictionary<string, QuestionPromptData> questionsById,
            Dictionary<string, QuestionPromptData> questionsByOriginalImporterKey,
            PawOrderQuestionUpdateReport report,
            bool applyChanges)
        {
            string canonicalQuestionId = BuildOriginalImporterQuestionId(question);
            string fileName = BuildOriginalImporterQuestionFileName(canonicalQuestionId, question.QuestionText);
            string originalImporterKey = BuildOriginalImporterQuestionKey(canonicalQuestionId, question.QuestionText);

            if (questionsById.TryGetValue(canonicalQuestionId, out QuestionPromptData existingQuestion) ||
                questionsByOriginalImporterKey.TryGetValue(originalImporterKey, out existingQuestion))
            {
                RegisterQuestionLookupKeys(questionsById, questionsByOriginalImporterKey, canonicalQuestionId, question.QuestionText, existingQuestion);
                report.RegisterExistingQuestion();
                return existingQuestion;
            }

            string path = questionsFolder + "/" + fileName;
            report.RegisterCreatedQuestion(path);

            if (!applyChanges)
            {
                return null;
            }

            QuestionPromptData questionAsset = ScriptableObject.CreateInstance<QuestionPromptData>();
            AssetDatabase.CreateAsset(questionAsset, path);

            SerializedObject serializedQuestion = new SerializedObject(questionAsset);
            serializedQuestion.FindProperty("questionId").stringValue = canonicalQuestionId;
            serializedQuestion.FindProperty("promptId").stringValue = canonicalQuestionId;
            serializedQuestion.FindProperty("displayName").stringValue = question.QuestionText;
            serializedQuestion.FindProperty("description").stringValue = question.QuestionText;
            serializedQuestion.FindProperty("promptType").enumValueIndex = (int)InvestigationPromptType.Question;
            serializedQuestion.FindProperty("visibleInPromptLists").boolValue = true;
            serializedQuestion.FindProperty("isStartingQuestion").boolValue = true;
            serializedQuestion.ApplyModifiedPropertiesWithoutUndo();

            RegisterQuestionLookupKeys(questionsById, questionsByOriginalImporterKey, canonicalQuestionId, question.QuestionText, questionAsset);
            return questionAsset;
        }

        private void ProcessResponse(
            PawOrderQuestionnaireQuestion question,
            KeyValuePair<string, string> responseByNpc,
            QuestionPromptData questionAsset,
            IReadOnlyDictionary<string, QuestionPromptData> questionsById,
            IReadOnlyDictionary<string, CharacterData> charactersByKey,
            IReadOnlyDictionary<string, ItemPromptData> itemsByKey,
            IReadOnlyDictionary<string, LocationData> locationsByKey,
            PawOrderQuestionUpdateReport report,
            bool applyChanges)
        {
            string response = responseByNpc.Value?.Trim();
            if (string.IsNullOrWhiteSpace(response))
            {
                return;
            }

            if (!TryResolveCharacter(responseByNpc.Key, charactersByKey, out CharacterData character))
            {
                if (!IsKnownNonCharacterResponseColumn(responseByNpc.Key, itemsByKey, locationsByKey))
                {
                    report.AddWarning("Character not found for response column: " + responseByNpc.Key);
                }

                return;
            }

            string characterName = GetCharacterDisplayName(character);
            string canonicalQuestionId = BuildOriginalImporterQuestionId(question);
            string outcomeId = BuildOutcomeId(canonicalQuestionId, characterName);
            string outcomePath = outcomesFolder + "/" + canonicalQuestionId + "/" + outcomeId + ".asset";
            InteractionOutcomeData outcome = AssetDatabase.LoadAssetAtPath<InteractionOutcomeData>(outcomePath);

            if (outcome == null)
            {
                report.RegisterCreatedOutcome(outcomePath);
                if (applyChanges)
                {
                    EnsureDirectory(outcomesFolder + "/" + canonicalQuestionId);
                    List<InvestigationPromptData> referencedPrompts = ResolveReferencedPrompts(question.QuestionText, response, itemsByKey, locationsByKey);
                    List<InvestigationPromptData> unlockedPrompts = ResolveUnlockQuestions(response, questionsById);
                    outcome = CreateOutcome(outcomePath, outcomeId, response, referencedPrompts, unlockedPrompts);
                }
            }
            else
            {
                report.RegisterExistingOutcome();
                if (!ResponseMatchesExistingOutcome(outcome, response))
                {
                    report.RegisterSkippedChangedResponse();
                    report.AddWarning("Existing outcome response differs and was not overwritten: " + outcomeId);
                }
            }

            string interactionId = BuildInteractionId(canonicalQuestionId, characterName);
            string interactionPath = interactionsFolder + "/" + characterName + "/" + interactionId + ".asset";
            CharacterInteractionData interaction = AssetDatabase.LoadAssetAtPath<CharacterInteractionData>(interactionPath);

            if (interaction == null)
            {
                report.RegisterCreatedInteraction(interactionPath);
                if (applyChanges && outcome != null)
                {
                    EnsureDirectory(interactionsFolder + "/" + characterName);
                    CreateInteraction(interactionPath, interactionId, character, questionAsset, outcome);
                }
            }
            else
            {
                report.RegisterExistingInteraction();
            }
        }

        private InteractionOutcomeData CreateOutcome(
            string path,
            string outcomeId,
            string response,
            List<InvestigationPromptData> referencedPrompts,
            List<InvestigationPromptData> unlockedPrompts)
        {
            InteractionOutcomeData outcome = ScriptableObject.CreateInstance<InteractionOutcomeData>();
            AssetDatabase.CreateAsset(outcome, path);

            SerializedObject serializedOutcome = new SerializedObject(outcome);
            serializedOutcome.FindProperty("outcomeId").stringValue = outcomeId;
            serializedOutcome.FindProperty("responseText").stringValue = response;
            serializedOutcome.FindProperty("proofSummary").stringValue = string.Empty;
            serializedOutcome.FindProperty("createsUsableProof").boolValue = false;
            serializedOutcome.FindProperty("marksPromptAsImportant").boolValue = false;
            SetObjectList(serializedOutcome.FindProperty("referencedPrompts"), referencedPrompts);
            SetObjectList(serializedOutcome.FindProperty("unlocksPrompts"), unlockedPrompts);
            serializedOutcome.ApplyModifiedPropertiesWithoutUndo();
            return outcome;
        }

        private void CreateInteraction(
            string path,
            string interactionId,
            CharacterData character,
            InvestigationPromptData prompt,
            InteractionOutcomeData outcome)
        {
            CharacterInteractionData interaction = ScriptableObject.CreateInstance<CharacterInteractionData>();
            AssetDatabase.CreateAsset(interaction, path);

            SerializedObject serializedInteraction = new SerializedObject(interaction);
            serializedInteraction.FindProperty("interactionId").stringValue = interactionId;
            serializedInteraction.FindProperty("targetCharacter").objectReferenceValue = character;
            serializedInteraction.FindProperty("promptUsed").objectReferenceValue = prompt;
            serializedInteraction.FindProperty("outcome").objectReferenceValue = outcome;
            serializedInteraction.FindProperty("canRepeat").boolValue = true;
            serializedInteraction.FindProperty("consumeInteraction").boolValue = false;
            ClearRequirements(serializedInteraction.FindProperty("requirements"));
            serializedInteraction.ApplyModifiedPropertiesWithoutUndo();
        }

        private void MergeQuestionsIntoCase(IReadOnlyList<QuestionPromptData> questions)
        {
            string casePath = casesFolder + "/PawOrder_MainCase.asset";
            CaseData caseData = AssetDatabase.LoadAssetAtPath<CaseData>(casePath);
            if (caseData == null)
            {
                caseData = ScriptableObject.CreateInstance<CaseData>();
                AssetDatabase.CreateAsset(caseData, casePath);
            }

            SerializedObject serializedCase = new SerializedObject(caseData);
            SerializedProperty caseId = serializedCase.FindProperty("caseId");
            if (caseId != null && string.IsNullOrWhiteSpace(caseId.stringValue))
            {
                caseId.stringValue = "PawOrder_MainCase";
            }

            SerializedProperty caseTitle = serializedCase.FindProperty("caseTitle");
            if (caseTitle != null && string.IsNullOrWhiteSpace(caseTitle.stringValue))
            {
                caseTitle.stringValue = "Paw & Order";
            }

            MergeObjectList(serializedCase.FindProperty("allPrompts"), questions.Cast<InvestigationPromptData>());
            IEnumerable<InvestigationPromptData> startingQuestions = questions.Where(IsStartingQuestion).Cast<InvestigationPromptData>();
            MergeObjectList(serializedCase.FindProperty("startingPrompts"), startingQuestions);
            serializedCase.ApplyModifiedPropertiesWithoutUndo();
        }

        private Dictionary<string, QuestionPromptData> LoadQuestionsById()
        {
            Dictionary<string, QuestionPromptData> result = new Dictionary<string, QuestionPromptData>(StringComparer.OrdinalIgnoreCase);
            foreach (QuestionPromptData question in LoadAssets<QuestionPromptData>(rootFolder))
            {
                string questionId = GetSerializedString(question, "questionId");
                if (!string.IsNullOrWhiteSpace(questionId) && !result.ContainsKey(questionId))
                {
                    result.Add(questionId, question);
                }

                string canonicalQuestionId = ExtractOriginalImporterQuestionId(questionId);
                if (!string.IsNullOrWhiteSpace(canonicalQuestionId) && !result.ContainsKey(canonicalQuestionId))
                {
                    result.Add(canonicalQuestionId, question);
                }
            }

            return result;
        }


        private Dictionary<string, QuestionPromptData> LoadQuestionsByOriginalImporterKey()
        {
            Dictionary<string, QuestionPromptData> result = new Dictionary<string, QuestionPromptData>(StringComparer.OrdinalIgnoreCase);
            foreach (QuestionPromptData question in LoadAssets<QuestionPromptData>(rootFolder))
            {
                string questionId = GetSerializedString(question, "questionId");
                string displayName = GetSerializedString(question, "displayName");
                string assetName = question.name;

                if (!string.IsNullOrWhiteSpace(questionId) && !string.IsNullOrWhiteSpace(displayName))
                {
                    AddLookupKey(result, BuildOriginalImporterQuestionKey(questionId, displayName), question);
                }

                if (!string.IsNullOrWhiteSpace(assetName))
                {
                    AddLookupKey(result, NormalizeQuestionAssetName(assetName), question);
                }
            }

            return result;
        }

        private static void RegisterQuestionLookupKeys(
            IDictionary<string, QuestionPromptData> questionsById,
            IDictionary<string, QuestionPromptData> questionsByOriginalImporterKey,
            string questionId,
            string questionText,
            QuestionPromptData question)
        {
            AddLookupKey(questionsById, questionId, question);
            AddLookupKey(questionsByOriginalImporterKey, BuildOriginalImporterQuestionKey(questionId, questionText), question);
        }

        private Dictionary<string, CharacterData> LoadCharactersByKey()
        {
            Dictionary<string, CharacterData> result = new Dictionary<string, CharacterData>(StringComparer.OrdinalIgnoreCase);
            foreach (CharacterData character in LoadAssets<CharacterData>(rootFolder))
            {
                AddLookupKey(result, GetSerializedString(character, "characterId"), character);
                AddLookupKey(result, GetSerializedString(character, "displayName"), character);
                AddLookupKey(result, character.name, character);
            }

            return result;
        }

        private Dictionary<string, ItemPromptData> LoadItemsByKey()
        {
            Dictionary<string, ItemPromptData> result = new Dictionary<string, ItemPromptData>(StringComparer.OrdinalIgnoreCase);
            foreach (ItemPromptData item in LoadAssets<ItemPromptData>(rootFolder))
            {
                AddLookupKey(result, GetSerializedString(item, "promptId"), item);
                AddLookupKey(result, GetSerializedString(item, "displayName"), item);
                AddLookupKey(result, item.name, item);
            }

            return result;
        }

        private Dictionary<string, LocationData> LoadLocationsByKey()
        {
            Dictionary<string, LocationData> result = new Dictionary<string, LocationData>(StringComparer.OrdinalIgnoreCase);
            foreach (LocationData location in LoadAssets<LocationData>(rootFolder))
            {
                AddLookupKey(result, GetSerializedString(location, "locationId"), location);
                AddLookupKey(result, GetSerializedString(location, "displayName"), location);
                AddLookupKey(result, location.name, location);
            }

            return result;
        }

        private static IEnumerable<T> LoadAssets<T>(string folder) where T : UnityEngine.Object
        {
            string[] searchFolders = AssetDatabase.IsValidFolder(folder) ? new[] { folder } : null;
            string[] guids = searchFolders == null ? AssetDatabase.FindAssets("t:" + typeof(T).Name) : AssetDatabase.FindAssets("t:" + typeof(T).Name, searchFolders);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    yield return asset;
                }
            }
        }

        private List<InvestigationPromptData> ResolveReferencedPrompts(
            string questionText,
            string response,
            IReadOnlyDictionary<string, ItemPromptData> itemsByKey,
            IReadOnlyDictionary<string, LocationData> locationsByKey)
        {
            List<InvestigationPromptData> result = new List<InvestigationPromptData>();
            HashSet<UnityEngine.Object> seen = new HashSet<UnityEngine.Object>();

            foreach (Match match in ItemReferenceRegex.Matches(response))
            {
                string itemCode = match.Groups[1].Value.ToUpperInvariant();
                if (itemsByKey.TryGetValue(itemCode, out ItemPromptData item) && item != null && seen.Add(item))
                {
                    result.Add(item);
                }
            }

            foreach (LocationData location in locationsByKey.Values.Distinct())
            {
                string displayName = GetSerializedString(location, "displayName");
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                if (!ContainsTerm(questionText, displayName) && !ContainsTerm(response, displayName))
                {
                    continue;
                }

                string referencePath = referenceLocationsFolder + "/LOC_" + ToSafeSlug(displayName).ToUpperInvariant() + ".asset";
                ReferencePromptData referencePrompt = AssetDatabase.LoadAssetAtPath<ReferencePromptData>(referencePath);
                if (referencePrompt != null && seen.Add(referencePrompt))
                {
                    result.Add(referencePrompt);
                }
            }

            return result;
        }

        private static List<InvestigationPromptData> ResolveUnlockQuestions(string response, IReadOnlyDictionary<string, QuestionPromptData> questionsById)
        {
            List<InvestigationPromptData> result = new List<InvestigationPromptData>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in SuperscriptUnlockRegex.Matches(response))
            {
                string digits = SuperscriptToDigits(match.Groups[1].Value);
                if (string.IsNullOrWhiteSpace(digits))
                {
                    continue;
                }

                string questionId = "Q" + digits.PadLeft(2, '0');
                if (seen.Add(questionId) && questionsById.TryGetValue(questionId, out QuestionPromptData question))
                {
                    result.Add(question);
                }
            }

            return result;
        }

        private static bool TryResolveCharacter(string sourceName, IReadOnlyDictionary<string, CharacterData> charactersByKey, out CharacterData character)
        {
            character = null;
            if (string.IsNullOrWhiteSpace(sourceName) || charactersByKey == null)
            {
                return false;
            }

            string trimmedSourceName = sourceName.Trim();
            if (charactersByKey.TryGetValue(trimmedSourceName, out character))
            {
                return true;
            }

            foreach (string candidateKey in BuildCharacterHeaderCandidates(trimmedSourceName))
            {
                if (!string.IsNullOrWhiteSpace(candidateKey) && charactersByKey.TryGetValue(candidateKey, out character))
                {
                    return true;
                }
            }

            foreach (KeyValuePair<string, CharacterData> characterByKey in charactersByKey)
            {
                if (string.IsNullOrWhiteSpace(characterByKey.Key) || characterByKey.Value == null)
                {
                    continue;
                }

                if (ContainsTerm(trimmedSourceName, characterByKey.Key))
                {
                    character = characterByKey.Value;
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> BuildCharacterHeaderCandidates(string header)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                yield break;
            }

            yield return header.Trim();
            yield return GetFirstTokenKey(header);
            yield return GetLastTokenKey(header);

            string cleanedHeader = Regex.Replace(header, "\\([^)]*\\)", " ");
            cleanedHeader = Regex.Replace(cleanedHeader, "(?i)\\b(resposta|response|fala|dialogo|diálogo|npc|personagem|character)\\b", " ");
            cleanedHeader = Regex.Replace(cleanedHeader, "[_\\-:;|/]+", " ");
            cleanedHeader = Regex.Replace(cleanedHeader, "\\s+", " ").Trim();

            yield return cleanedHeader;
            yield return GetFirstTokenKey(cleanedHeader);
            yield return GetLastTokenKey(cleanedHeader);

            foreach (string token in Regex.Split(cleanedHeader, "\\s+"))
            {
                if (!string.IsNullOrWhiteSpace(token))
                {
                    yield return token.Trim();
                }
            }
        }

        private static bool IsKnownNonCharacterResponseColumn(
            string sourceName,
            IReadOnlyDictionary<string, ItemPromptData> itemsByKey,
            IReadOnlyDictionary<string, LocationData> locationsByKey)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
            {
                return true;
            }

            string trimmedSourceName = sourceName.Trim();
            return itemsByKey.ContainsKey(trimmedSourceName) || locationsByKey.ContainsKey(trimmedSourceName);
        }

        private static bool ResponseMatchesExistingOutcome(InteractionOutcomeData outcome, string response)
        {
            string existingResponse = GetSerializedString(outcome, "responseText")?.Trim() ?? string.Empty;
            return string.Equals(existingResponse, response?.Trim() ?? string.Empty, StringComparison.Ordinal);
        }

        private static bool IsStartingQuestion(QuestionPromptData question)
        {
            if (question == null)
            {
                return false;
            }

            SerializedObject serializedQuestion = new SerializedObject(question);
            return serializedQuestion.FindProperty("isStartingQuestion")?.boolValue == true;
        }

        private static string GetCharacterDisplayName(CharacterData character)
        {
            string displayName = GetSerializedString(character, "displayName");
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName.Trim();
            }

            return character != null ? character.name : string.Empty;
        }

        private static string GetSerializedString(UnityEngine.Object target, string propertyName)
        {
            if (target == null)
            {
                return string.Empty;
            }

            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property == null ? string.Empty : property.stringValue;
        }

        private static void AddLookupKey<T>(IDictionary<string, T> lookup, string key, T value) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(key) || value == null)
            {
                return;
            }

            string trimmedKey = key.Trim();
            if (!lookup.ContainsKey(trimmedKey))
            {
                lookup.Add(trimmedKey, value);
            }

            string firstToken = GetFirstTokenKey(trimmedKey);
            if (!string.IsNullOrWhiteSpace(firstToken) && !lookup.ContainsKey(firstToken))
            {
                lookup.Add(firstToken, value);
            }
        }

        private static void ClearRequirements(SerializedProperty requirements)
        {
            if (requirements == null)
            {
                return;
            }

            requirements.FindPropertyRelative("requiredKnownPrompts").arraySize = 0;
            requirements.FindPropertyRelative("requiredKnownOutcomes").arraySize = 0;
            requirements.FindPropertyRelative("requiredCollectedItems").arraySize = 0;
            requirements.FindPropertyRelative("requiredUnlockedLocations").arraySize = 0;
        }

        private static void SetObjectList<T>(SerializedProperty property, IList<T> values) where T : UnityEngine.Object
        {
            if (property == null)
            {
                return;
            }

            List<T> safeValues = values == null ? new List<T>() : values.Where(value => value != null).Distinct().ToList();
            property.arraySize = safeValues.Count;
            for (int index = 0; index < safeValues.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = safeValues[index];
            }
        }

        private static void MergeObjectList<T>(SerializedProperty property, IEnumerable<T> valuesToAdd) where T : UnityEngine.Object
        {
            if (property == null)
            {
                return;
            }

            List<T> merged = new List<T>();
            HashSet<T> seen = new HashSet<T>();
            for (int index = 0; index < property.arraySize; index++)
            {
                T existing = property.GetArrayElementAtIndex(index).objectReferenceValue as T;
                if (existing != null && seen.Add(existing))
                {
                    merged.Add(existing);
                }
            }

            foreach (T value in valuesToAdd ?? Enumerable.Empty<T>())
            {
                if (value != null && seen.Add(value))
                {
                    merged.Add(value);
                }
            }

            SetObjectList(property, merged);
        }

        private static void EnsureDirectory(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
            string name = Path.GetFileName(folder);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            EnsureDirectory(parent);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static string NormalizeAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return string.Empty;
            }

            return folder.Trim().Replace("\\", "/").TrimEnd('/');
        }

        private static string BuildOutcomeId(string promptId, string characterName)
        {
            return "Outcome_" + promptId + "_" + characterName;
        }

        private static string BuildInteractionId(string promptId, string characterName)
        {
            return "Interaction_" + promptId + "_" + characterName;
        }


        private static string BuildOriginalImporterQuestionId(PawOrderQuestionnaireQuestion question)
        {
            string rawQuestionId = question?.RawQuestionId?.Trim() ?? string.Empty;
            string parsedQuestionId = question?.QuestionId?.Trim() ?? string.Empty;

            string normalizedRawQuestionId = ExtractOriginalImporterQuestionId(rawQuestionId);
            if (!string.IsNullOrWhiteSpace(normalizedRawQuestionId))
            {
                return normalizedRawQuestionId;
            }

            string normalizedParsedQuestionId = ExtractOriginalImporterQuestionId(parsedQuestionId);
            if (!string.IsNullOrWhiteSpace(normalizedParsedQuestionId))
            {
                return normalizedParsedQuestionId;
            }

            string stablePrefix = ToSafeSlug(string.IsNullOrWhiteSpace(parsedQuestionId) ? rawQuestionId : parsedQuestionId).ToUpperInvariant();
            string stableSuffix = ToSafeSlug(question?.QuestionText ?? string.Empty);
            if (string.IsNullOrWhiteSpace(stablePrefix) || stablePrefix == "ENTRY")
            {
                return stableSuffix.ToUpperInvariant();
            }

            return stablePrefix + "_" + stableSuffix;
        }

        private static string ExtractOriginalImporterQuestionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmedValue = value.Trim();
            Match questionCodeMatch = QuestionCodeRegex.Match(trimmedValue);
            if (questionCodeMatch.Success)
            {
                return "Q" + questionCodeMatch.Groups[1].Value.PadLeft(2, '0');
            }

            Match parenthesizedCodeMatch = ParenthesizedQuestionCodeRegex.Match(trimmedValue);
            if (parenthesizedCodeMatch.Success)
            {
                string digits = SuperscriptToDigits(parenthesizedCodeMatch.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(digits))
                {
                    return "Q" + digits.PadLeft(2, '0');
                }
            }

            return string.Empty;
        }

        private static string BuildOriginalImporterQuestionFileName(string questionId, string questionText)
        {
            return questionId + "_" + ToSafeSlug(questionText) + ".asset";
        }

        private static string BuildOriginalImporterQuestionKey(string questionId, string questionText)
        {
            return NormalizeQuestionAssetName(Path.GetFileNameWithoutExtension(BuildOriginalImporterQuestionFileName(questionId, questionText)));
        }

        private static string NormalizeQuestionAssetName(string value)
        {
            return NormalizeLookupKey(value).Replace(" ", "_");
        }

        private static bool ContainsTerm(string text, string term)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(term))
            {
                return false;
            }

            return NormalizeLookupKey(text).Contains(NormalizeLookupKey(term), StringComparison.Ordinal);
        }

        private static string SuperscriptToDigits(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            foreach (char character in value)
            {
                builder.Append(character switch
                {
                    '\u2070' => '0',
                    '\u00B9' => '1',
                    '\u00B2' => '2',
                    '\u00B3' => '3',
                    '\u2074' => '4',
                    '\u2075' => '5',
                    '\u2076' => '6',
                    '\u2077' => '7',
                    '\u2078' => '8',
                    '\u2079' => '9',
                    _ => char.IsDigit(character) ? character : '\0'
                });
            }

            return builder.ToString().Replace("\0", string.Empty);
        }

        private static string GetFirstTokenKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmedValue = value.Trim();
            int spaceIndex = trimmedValue.IndexOf(' ');
            return spaceIndex <= 0 ? trimmedValue : trimmedValue.Substring(0, spaceIndex).Trim();
        }

        private static string GetLastTokenKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmedValue = value.Trim();
            int spaceIndex = trimmedValue.LastIndexOf(' ');
            return spaceIndex <= 0 || spaceIndex >= trimmedValue.Length - 1 ? trimmedValue : trimmedValue.Substring(spaceIndex + 1).Trim();
        }

        private static string ToSafeSlug(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "ENTRY";
            }

            string normalized = value.Normalize(NormalizationForm.FormD);
            StringBuilder builder = new StringBuilder();
            foreach (char character in normalized)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                builder.Append(char.IsLetterOrDigit(character) ? character : '_');
            }

            string cleaned = Regex.Replace(builder.ToString(), "_+", "_").Trim('_');
            return string.IsNullOrWhiteSpace(cleaned) ? "ENTRY" : cleaned;
        }

        private static string NormalizeLookupKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Normalize(NormalizationForm.FormD);
            StringBuilder builder = new StringBuilder(normalized.Length);
            foreach (char character in normalized)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(char.ToUpperInvariant(character));
                }
            }

            return builder.ToString();
        }

        private static string BuildCompletionMessage(bool applyChanges)
        {
            return applyChanges ? "Question update applied without overwriting existing configured outcomes." : "Question update preview completed.";
        }

        private readonly struct ResponseColumnDefinition
        {
            #region Constructors

            public ResponseColumnDefinition(int columnIndex, string characterName)
            {
                ColumnIndex = columnIndex;
                CharacterName = characterName;
            }

            #endregion

            #region Properties

            public int ColumnIndex { get; }
            public string CharacterName { get; }

            #endregion
        }

        #endregion
    }
}
