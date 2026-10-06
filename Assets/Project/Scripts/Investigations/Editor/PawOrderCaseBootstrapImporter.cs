using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace Fabula.PawOrder.Editor
{
    public static class PawOrderCaseBootstrapImporter
    {
        private static readonly string XlsxPath = @"D:/Projetos Unity/Fabula/Documents/Paw&Order/FalemeSobreX_OrdemFinalPerguntas.xlsx";
        private static readonly string BlueCanaryWorkbookPath = @"Assets/Project/Documents/blue-canary-personagens.xlsx";
        private const string RequiredLocationsSheetName = "Locations";
        private const string RequiredOverviewSheetName = "Visao Geral";
        private const string RequiredItemsSheetName = "Itens";

        private const string Root = "Assets/Project/Scriptables/Investigations/PawOrder";
        private const string CharactersDir = Root + "/Characters";
        private const string LocationsDir = Root + "/Locations";
        private const string ItemsDir = Root + "/Items";
        private const string QuestionsDir = Root + "/Questions";
        private const string ReferencesDir = Root + "/References";
        private const string ReferenceCharactersDir = ReferencesDir + "/Characters";
        private const string ReferenceLocationsDir = ReferencesDir + "/Locations";
        private const string ReferenceFactsDir = ReferencesDir + "/Facts";
        private const string OutcomesDir = Root + "/Outcomes";
        private const string InteractionsDir = Root + "/Interactions";
        private const string CasesDir = Root + "/Cases";

        private const string TopicSheetName = "Planilha1";
        private const string QuestionSheetName = "Mapa Perguntas Final";

        private static readonly string[] CharacterOrder = { "Milo", "Nina", "Vera", "Otto", "Greta", "Boris" };
        private static readonly Regex ItemReferenceRegex = new(@"\[(I\d+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SuperscriptUnlockRegex = new(@"\(([0-9\u2070\u00B9\u00B2\u00B3\u2074\u2075\u2076\u2077\u2078\u2079]+)\)", RegexOptions.Compiled);
        private static readonly Regex QuestionIdRegex = new(@"Q\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly HashSet<string> CharacterTopics = new(StringComparer.OrdinalIgnoreCase)
        {
            "Lola", "Greta", "Milo", "Nina", "Vera", "Otto", "Boris"
        };
        private static readonly HashSet<string> LocationTopics = new(StringComparer.OrdinalIgnoreCase)
        {
            "Camarim de Lola",
            "Palco do Blue Canary",
            "Bar",
            "Beco dos Fundos",
            "EscritÃ³rio de Greta",
            "Escritorio de Greta"
        };
        private static readonly HashSet<string> ItemTopics = new(StringComparer.OrdinalIgnoreCase)
        {
            "Batom de Lola com base raspada",
            "LenÃ§o de retoque com pigmento borrado",
            "Lenco de retoque com pigmento borrado",
            "Caixa de entrega aberta com etiqueta arrancada",
            "TaÃ§a de Ã¡gua parcialmente cheia",
            "Taca de agua parcialmente cheia",
            "Copo com marca de batom que nÃ£o Ã© da Lola",
            "Copo com marca de batom que nao e da Lola",
            "Frasco de perfume caro",
            "Caixa de presente com fita dourada",
            "Gravata borboleta amassada",
            "Broche elegante quebrado",
            "Kit de costura improvisado",
            "Microfone com ajuste alterado",
            "Livro-caixa com inconsistÃªncias",
            "Livro-caixa com inconsistencias",
            "Chave duplicada do camarim sem identificaÃ§Ã£o clara",
            "Chave duplicada do camarim sem identificacao clara",
            "Contrato parcialmente queimado"
        };

        [MenuItem("Fabula/Paw Order/Investigations/Import Paw & Order Case Assets")]
        public static void ImportFromMenu() => ImportFromSpreadsheet();

        public static void ImportFromSpreadsheet()
        {
            if (!File.Exists(XlsxPath))
            {
                Debug.LogError($"PawOrder import failed: XLSX not found at '{XlsxPath}'.");
                return;
            }

            EnsureDirectoryStructure();
            var workbook = ParseWorkbook(XlsxPath);
            var truthData = LoadTruthWorkbookData();

            if (truthData.CharacterNames.Count > 0)
            {
                Debug.Log($"PawOrder import: truth workbook characters detected: {string.Join(", ", truthData.CharacterNames)}.");
            }

            var characters = EnsureCharacters();
            var mergedItemRows = MergeItemRows(truthData.ItemRows, workbook.Questions.Items);
            var items = EnsureItems(mergedItemRows);
            var uniqueItems = GetDistinctPromptsById(items.Values);
            var truthLocations = truthData.LocationEntries;
            var locations = EnsureLocations(workbook.Topics.Rows, truthLocations, characters, items);
            var questions = EnsureQuestions(workbook.Questions.Rows);
            var referencePrompts = EnsureReferencePrompts(workbook.Topics.Rows, characters, locations, items);

            var unlockedQuestionIds = CollectUnlockedQuestionIdsFromResponses(workbook.Questions.Rows);
            ApplyStartingQuestions(questions, unlockedQuestionIds);

            var questionOutcomes = CreateInteractionsForQuestions(workbook.Questions.Rows, questions, items, characters, locations);
            var referenceOutcomes = CreateInteractionsForReferencePrompts(workbook.Topics.Rows, referencePrompts, items, characters);

            var allPrompts = new List<InvestigationPromptData>();
            allPrompts.AddRange(questions.Values.OrderBy(q => q.QuestionId, StringComparer.OrdinalIgnoreCase));
            allPrompts.AddRange(uniqueItems.OrderBy(i => i.PromptId, StringComparer.OrdinalIgnoreCase));
            allPrompts.AddRange(referencePrompts.Values.OrderBy(r => r.PromptId, StringComparer.OrdinalIgnoreCase));

            CreateOrUpdateCase(characters, locations, allPrompts, questions.Values.ToList(), referencePrompts.Values.ToList());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var outcomeCount = questionOutcomes + referenceOutcomes;
            var interactionCount = outcomeCount;
            Debug.Log($"PawOrder import done. Items: {uniqueItems.Count} unique assets ({items.Count} lookup keys), Questions: {questions.Count}, References: {referencePrompts.Count}, Outcomes: {outcomeCount}, Interactions: {interactionCount}.");
        }

        private static Dictionary<string, CharacterData> EnsureCharacters()
        {
            var result = new Dictionary<string, CharacterData>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in CharacterOrder)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var cleanName = name.Trim();
                var character = FindCharacterByNameOrId(cleanName);
                if (character == null)
                {
                    var path = $"{CharactersDir}/{cleanName}.asset";
                    character = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
                    if (character == null)
                    {
                        character = ScriptableObject.CreateInstance<CharacterData>();
                        AssetDatabase.CreateAsset(character, path);
                    }
                }

                var so = new SerializedObject(character);
                so.FindProperty("characterId").stringValue = cleanName;
                so.FindProperty("displayName").stringValue = cleanName;
                so.FindProperty("isSuspect").boolValue = false;
                so.FindProperty("canBeChosenAsCulprit").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();

                AddCharacterLookupKeys(result, cleanName, character);
            }

            return result;
        }

        private static void AddCharacterLookupKeys(
            IDictionary<string, CharacterData> lookup,
            string sourceName,
            CharacterData character)
        {
            if (character == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(sourceName))
            {
                lookup[sourceName.Trim()] = character;
            }

            var so = new SerializedObject(character);
            var id = so.FindProperty("characterId")?.stringValue;
            var display = so.FindProperty("displayName")?.stringValue;

            if (!string.IsNullOrWhiteSpace(id))
            {
                lookup[id.Trim()] = character;
                var idFirstToken = GetFirstTokenKey(id);
                if (!string.IsNullOrWhiteSpace(idFirstToken))
                {
                    lookup[idFirstToken] = character;
                }
            }

            if (!string.IsNullOrWhiteSpace(display))
            {
                lookup[display.Trim()] = character;
                var displayFirstToken = GetFirstTokenKey(display);
                if (!string.IsNullOrWhiteSpace(displayFirstToken))
                {
                    lookup[displayFirstToken] = character;
                }
            }
        }

        private static CharacterData FindCharacterByNameOrId(string characterName)
        {
            var guids = AssetDatabase.FindAssets("t:CharacterData");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
                if (asset == null)
                {
                    continue;
                }

                var so = new SerializedObject(asset);
                var id = so.FindProperty("characterId")?.stringValue;
                var display = so.FindProperty("displayName")?.stringValue;
                if (string.Equals(id, characterName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(display, characterName, StringComparison.OrdinalIgnoreCase))
                {
                    return asset;
                }
            }

            return null;
        }

        private static Dictionary<string, LocationData> LoadAllLocations()
        {
            var result = new Dictionary<string, LocationData>(StringComparer.OrdinalIgnoreCase);
            var guids = AssetDatabase.FindAssets("t:LocationData");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var location = AssetDatabase.LoadAssetAtPath<LocationData>(path);
                if (location == null)
                {
                    continue;
                }

                var so = new SerializedObject(location);
                var id = so.FindProperty("locationId")?.stringValue;
                var display = so.FindProperty("displayName")?.stringValue;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    result[id] = location;
                }

                if (!string.IsNullOrWhiteSpace(display))
                {
                    result[display] = location;
                }
            }

            return result;
        }

        private static Dictionary<string, LocationData> EnsureLocations(
            List<TopicRow> topicRows,
            IReadOnlyCollection<TruthLocationImportEntry> truthLocations,
            IReadOnlyDictionary<string, CharacterData> characters,
            IReadOnlyDictionary<string, ItemPromptData> items)
        {
            var result = LoadAllLocations();
            var detectedFromTopics = 0;
            var detectedFromTruth = truthLocations.Count;
            var created = 0;

            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var truthByLocation = new Dictionary<string, TruthLocationImportEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in topicRows)
            {
                if (!IsLikelyLocationTopic(row.TopicName))
                {
                    continue;
                }
                detectedFromTopics++;
                candidates.Add(row.TopicName);
            }

            foreach (var entry in truthLocations)
            {
                if (string.IsNullOrWhiteSpace(entry.LocationName))
                {
                    continue;
                }

                candidates.Add(entry.LocationName);
                truthByLocation[entry.LocationName] = entry;
            }

            foreach (var locationName in candidates)
            {
                var location = TryResolveLocation(locationName, result);
                if (location == null)
                {
                    var fileName = $"{ToSafeSlug(locationName)}.asset";
                    var path = $"{LocationsDir}/{fileName}";
                    location = AssetDatabase.LoadAssetAtPath<LocationData>(path);
                    if (location == null)
                    {
                        location = ScriptableObject.CreateInstance<LocationData>();
                        AssetDatabase.CreateAsset(location, path);
                        created++;
                    }
                }

                var so = new SerializedObject(location);
                so.FindProperty("locationId").stringValue = locationName;
                so.FindProperty("displayName").stringValue = locationName;
                if (TryResolveTruthLocationEntry(locationName, truthByLocation, out var truthEntry))
                {
                    MergeCharactersIntoLocation(so.FindProperty("charactersPresent"), truthEntry.CharacterNames, characters);
                    MergeItemsIntoLocation(so.FindProperty("collectibleItems"), truthEntry.ItemNames, items);
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                result[locationName] = location;
            }

            Debug.Log($"PawOrder import: detected {detectedFromTopics} locations from topic sheet, {detectedFromTruth} from truth workbook, created {created} LocationData assets, total indexed locations {result.Count}.");

            return result;
        }

        private static LocationData TryResolveLocation(
            string locationName,
            IReadOnlyDictionary<string, LocationData> locations)
        {
            if (string.IsNullOrWhiteSpace(locationName))
            {
                return null;
            }

            if (locations.TryGetValue(locationName, out var exact))
            {
                return exact;
            }

            var normalized = NormalizeLookupKey(locationName);
            foreach (var pair in locations)
            {
                if (NormalizeLookupKey(pair.Key) == normalized)
                {
                    return pair.Value;
                }
            }

            return null;
        }

        private static bool TryResolveTruthLocationEntry(
            string locationName,
            IReadOnlyDictionary<string, TruthLocationImportEntry> truthByLocation,
            out TruthLocationImportEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(locationName))
            {
                return false;
            }

            if (truthByLocation.TryGetValue(locationName, out entry))
            {
                return true;
            }

            var normalized = NormalizeLookupKey(locationName);
            foreach (var pair in truthByLocation)
            {
                if (NormalizeLookupKey(pair.Key) == normalized)
                {
                    entry = pair.Value;
                    return true;
                }
            }

            return false;
        }

        private static TruthWorkbookImportData LoadTruthWorkbookData()
        {
            if (!File.Exists(BlueCanaryWorkbookPath))
            {
                Debug.LogWarning($"PawOrder import: workbook not found at '{BlueCanaryWorkbookPath}'.");
                return new TruthWorkbookImportData();
            }

            try
            {
                var workbook = PawOrderExcelWorkbookReader.Load(BlueCanaryWorkbookPath);
                var data = new TruthWorkbookImportData();
                var entries = new Dictionary<string, TruthLocationImportEntry>(StringComparer.OrdinalIgnoreCase);
                LoadCharactersFromOverviewSheet(workbook, data.CharacterNames);
                LoadItemsFromItemsSheet(workbook, data.ItemRows);
                LoadItemLocationAndCharactersFromItemsSheet(workbook, entries);
                if (TryLoadLocationsFromDedicatedSheet(workbook, entries, out var dedicatedSheetName))
                {
                    data.LocationEntries.AddRange(entries.Values);
                    Debug.Log($"PawOrder import: loaded {data.CharacterNames.Count} characters, {data.ItemRows.Count} items and {entries.Count} locations from '{Path.GetFileName(BlueCanaryWorkbookPath)}'.");
                    return data;
                }
                var available = string.Join(", ", workbook.SheetsByName.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
                Debug.LogWarning($"PawOrder import: required sheet '{RequiredLocationsSheetName}' was not found in '{Path.GetFileName(BlueCanaryWorkbookPath)}'. Available sheets: {available}.");
                return data;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"PawOrder import: failed loading locations from '{Path.GetFileName(BlueCanaryWorkbookPath)}'. {exception.Message}");
                return new TruthWorkbookImportData();
            }
        }

        private static void LoadCharactersFromOverviewSheet(PawOrderExcelWorkbookReader workbook, ICollection<string> characterNames)
        {
            if (!workbook.TryGetSheet(RequiredOverviewSheetName, out var rows))
            {
                return;
            }

            for (var rowNumber = 4; rowNumber <= 9; rowNumber++)
            {
                var rowIndex = rowNumber - 1;
                if (rowIndex < 0 || rowIndex >= rows.Count)
                {
                    continue;
                }

                var row = rows[rowIndex];
                var name = row.Count > 0 ? row[0]?.Trim() : string.Empty;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    characterNames.Add(name);
                }
            }
        }

        private static void LoadItemsFromItemsSheet(PawOrderExcelWorkbookReader workbook, ICollection<ItemRow> items)
        {
            if (!workbook.TryGetSheet(RequiredItemsSheetName, out var rows))
            {
                return;
            }

            var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var rowNumber = 4; rowNumber <= 17; rowNumber++)
            {
                var rowIndex = rowNumber - 1;
                if (rowIndex < 0 || rowIndex >= rows.Count)
                {
                    continue;
                }

                var row = rows[rowIndex];
                var itemName = ReadRowCell(row, 1);
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    continue;
                }

                var code = $"I{(rowNumber - 3):D2}";
                if (!seenCodes.Add(code))
                {
                    continue;
                }

                items.Add(new ItemRow
                {
                    ItemCode = code,
                    ItemName = itemName
                });
            }
        }

        private static void LoadItemLocationAndCharactersFromItemsSheet(
            PawOrderExcelWorkbookReader workbook,
            Dictionary<string, TruthLocationImportEntry> entriesByLocation)
        {
            if (!workbook.TryGetSheet(RequiredItemsSheetName, out var rows))
            {
                return;
            }

            for (var rowNumber = 4; rowNumber <= 17; rowNumber++)
            {
                var rowIndex = rowNumber - 1;
                if (rowIndex < 0 || rowIndex >= rows.Count)
                {
                    continue;
                }

                var row = rows[rowIndex];
                var itemName = ReadRowCell(row, 1);
                var locationName = ReadRowCell(row, 2);
                if (string.IsNullOrWhiteSpace(itemName) || string.IsNullOrWhiteSpace(locationName))
                {
                    continue;
                }

                var involvedE = SplitPotentialList(ReadRowCell(row, 5));
                var involvedF = SplitPotentialList(ReadRowCell(row, 6));
                var characters = involvedE.Concat(involvedF).ToList();
                AddTruthLocationEntry(entriesByLocation, locationName, characters, new[] { itemName });
            }
        }
        private static bool TryLoadLocationsFromDedicatedSheet(
            PawOrderExcelWorkbookReader workbook,
            Dictionary<string, TruthLocationImportEntry> entriesByLocation,
            out string sheetName)
        {
            sheetName = string.Empty;
            var locationSheet = workbook.SheetsByName.FirstOrDefault(pair =>
            {
                var key = NormalizeLookupKey(pair.Key);
                return key == NormalizeLookupKey(RequiredLocationsSheetName);
            });

            if (string.IsNullOrWhiteSpace(locationSheet.Key) || locationSheet.Value == null || locationSheet.Value.Count == 0)
            {
                return false;
            }

            var rows = locationSheet.Value;
            for (var rowNumber = 4; rowNumber <= 8; rowNumber++)
            {
                var rowIndex = rowNumber - 1;
                if (rowIndex < 0 || rowIndex >= rows.Count)
                {
                    continue;
                }

                var row = rows[rowIndex];
                var locationName = ReadRowCell(row, 1);
                if (string.IsNullOrWhiteSpace(locationName))
                {
                    continue;
                }

                var characterNames = SplitPotentialList(ReadRowCell(row, 2)).ToList();
                var itemNames = SplitPotentialList(ReadRowCell(row, 4)).ToList();
                AddTruthLocationEntry(entriesByLocation, locationName, characterNames, itemNames);
            }

            sheetName = locationSheet.Key;
            return entriesByLocation.Count > 0;
        }

        private static string ReadRowCell(IReadOnlyList<string> row, int oneBasedColumnIndex)
        {
            var zeroBased = oneBasedColumnIndex - 1;
            if (row == null || zeroBased < 0 || zeroBased >= row.Count)
            {
                return string.Empty;
            }

            return row[zeroBased]?.Trim() ?? string.Empty;
        }

        private static void AddTruthLocationEntry(
            Dictionary<string, TruthLocationImportEntry> entriesByLocation,
            string locationName,
            IEnumerable<string> characterNames,
            IEnumerable<string> itemNames)
        {
            if (string.IsNullOrWhiteSpace(locationName))
            {
                return;
            }

            if (!entriesByLocation.TryGetValue(locationName, out var entry))
            {
                entry = new TruthLocationImportEntry(locationName);
                entriesByLocation[locationName] = entry;
            }

            foreach (var characterName in characterNames)
            {
                if (!string.IsNullOrWhiteSpace(characterName))
                {
                    entry.CharacterNames.Add(characterName.Trim());
                }
            }

            foreach (var itemName in itemNames)
            {
                if (!string.IsNullOrWhiteSpace(itemName))
                {
                    entry.ItemNames.Add(itemName.Trim());
                }
            }
        }

        private static void AddTruthLocationEntry(
            Dictionary<string, TruthLocationImportEntry> entriesByLocation,
            string locationName,
            string characterName)
        {
            AddTruthLocationEntry(entriesByLocation, locationName, new[] { characterName }, Array.Empty<string>());
        }

        private static void MergeCharactersIntoLocation(
            SerializedProperty charactersProperty,
            IReadOnlyCollection<string> characterNames,
            IReadOnlyDictionary<string, CharacterData> characters)
        {
            if (charactersProperty == null || characterNames == null || characterNames.Count == 0)
            {
                return;
            }

            var merged = new List<CharacterData>();
            var seen = new HashSet<CharacterData>();

            for (var i = 0; i < charactersProperty.arraySize; i++)
            {
                var existing = charactersProperty.GetArrayElementAtIndex(i).objectReferenceValue as CharacterData;
                if (existing != null && seen.Add(existing))
                {
                    merged.Add(existing);
                }
            }

            foreach (var name in characterNames)
            {
                if (TryResolveCharacter(name, characters, out var character) && seen.Add(character))
                {
                    merged.Add(character);
                }
            }

            SetObjectList(charactersProperty, merged);
        }

        private static void MergeItemsIntoLocation(
            SerializedProperty itemsProperty,
            IReadOnlyCollection<string> itemNames,
            IReadOnlyDictionary<string, ItemPromptData> items)
        {
            if (itemsProperty == null || itemNames == null || itemNames.Count == 0)
            {
                return;
            }

            var merged = new List<ItemPromptData>();
            var seen = new HashSet<ItemPromptData>();

            for (var i = 0; i < itemsProperty.arraySize; i++)
            {
                var existing = itemsProperty.GetArrayElementAtIndex(i).objectReferenceValue as ItemPromptData;
                if (existing != null && seen.Add(existing))
                {
                    merged.Add(existing);
                }
            }

            foreach (var name in itemNames)
            {
                if (TryResolveItem(name, items, out var item) && seen.Add(item))
                {
                    merged.Add(item);
                }
            }

            SetObjectList(itemsProperty, merged);
        }

        private static bool TryResolveCharacter(
            string rawName,
            IReadOnlyDictionary<string, CharacterData> characters,
            out CharacterData character)
        {
            character = null;
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return false;
            }

            var clean = rawName.Trim();
            if (characters.TryGetValue(clean, out character))
            {
                return true;
            }

            var normalized = NormalizeLookupKey(clean);
            foreach (var pair in characters)
            {
                if (NormalizeLookupKey(pair.Key) == normalized)
                {
                    character = pair.Value;
                    return true;
                }
            }

            var firstToken = GetFirstTokenKey(clean);
            if (!string.IsNullOrWhiteSpace(firstToken))
            {
                foreach (var pair in characters)
                {
                    if (GetFirstTokenKey(pair.Key) == firstToken)
                    {
                        character = pair.Value;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryResolveItem(
            string rawName,
            IReadOnlyDictionary<string, ItemPromptData> items,
            out ItemPromptData item)
        {
            item = null;
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return false;
            }

            var clean = rawName.Trim();
            if (items.TryGetValue(clean, out item))
            {
                return true;
            }

            var normalized = NormalizeLookupKey(clean);
            foreach (var pair in items)
            {
                if (NormalizeLookupKey(pair.Key) == normalized)
                {
                    item = pair.Value;
                    return true;
                }
            }

            return false;
        }

        private static int FindColumnByAnyToken(IReadOnlyList<string> header, params string[] tokens)
        {
            for (var i = 0; i < header.Count; i++)
            {
                var key = NormalizeLookupKey(header[i]);
                foreach (var token in tokens)
                {
                    if (key.Contains(token, StringComparison.Ordinal))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static IEnumerable<string> SplitPotentialList(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            var normalized = value.Replace("\n", ",").Replace(";", ",").Replace("|", ",");
            foreach (var part in normalized.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var clean = part.Trim();
                if (!string.IsNullOrWhiteSpace(clean))
                {
                    yield return clean;
                }
            }
        }

        private static bool IsLikelyLocationTopic(string topicName)
        {
            if (string.IsNullOrWhiteSpace(topicName))
            {
                return false;
            }

            // 1) Keep compatibility with the existing explicit classification.
            if (ClassifyTopic(topicName).Category == TopicCategory.Location)
            {
                return true;
            }

            // 2) Normalize to survive accent/encoding variations.
            var normalizedTopic = NormalizeLookupKey(topicName);
            foreach (var known in LocationTopics)
            {
                if (normalizedTopic == NormalizeLookupKey(known))
                {
                    return true;
                }
            }

            // 3) Lightweight keyword fallback used by the sheet labels.
            return normalizedTopic.Contains("camarim", StringComparison.Ordinal) ||
                   normalizedTopic.Contains("palco", StringComparison.Ordinal) ||
                   normalizedTopic.Contains("beco", StringComparison.Ordinal) ||
                   normalizedTopic == "bar" ||
                   normalizedTopic.Contains("escritorio", StringComparison.Ordinal) ||
                   normalizedTopic.Contains("escritorio", StringComparison.Ordinal);
        }

        private static Dictionary<string, ItemPromptData> EnsureItems(List<ItemRow> itemRows)
        {
            var result = new Dictionary<string, ItemPromptData>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in itemRows)
            {
                var fileName = $"{row.ItemCode.ToUpperInvariant()}.asset";
                var path = $"{ItemsDir}/{fileName}";
                var item = FindItemByPromptId(row.ItemCode) ?? AssetDatabase.LoadAssetAtPath<ItemPromptData>(path);
                if (item == null)
                {
                    item = ScriptableObject.CreateInstance<ItemPromptData>();
                    AssetDatabase.CreateAsset(item, path);
                }

                var so = new SerializedObject(item);
                so.FindProperty("promptId").stringValue = row.ItemCode;
                so.FindProperty("displayName").stringValue = row.ItemName;
                so.FindProperty("promptType").enumValueIndex = (int)InvestigationPromptType.Item;
                so.ApplyModifiedPropertiesWithoutUndo();

                result[row.ItemCode] = item;
                result[row.ItemName] = item;
            }

            return result;
        }

        private static ItemPromptData FindItemByPromptId(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId))
            {
                return null;
            }

            var normalizedPromptId = promptId.Trim();
            var guids = AssetDatabase.FindAssets("t:ItemPromptData");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemPromptData>(path);
                if (item == null)
                {
                    continue;
                }

                var so = new SerializedObject(item);
                var itemPromptId = so.FindProperty("promptId")?.stringValue;
                if (string.Equals(itemPromptId, normalizedPromptId, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        private static List<ItemRow> MergeItemRows(
            IReadOnlyCollection<ItemRow> truthItems,
            IReadOnlyList<ItemRow> legendItems)
        {
            var mergedByCode = new Dictionary<string, ItemRow>(StringComparer.OrdinalIgnoreCase);

            if (legendItems != null)
            {
                foreach (var legendItem in legendItems)
                {
                    if (legendItem == null || string.IsNullOrWhiteSpace(legendItem.ItemCode))
                    {
                        continue;
                    }

                    var code = legendItem.ItemCode.Trim().ToUpperInvariant();
                    if (string.IsNullOrWhiteSpace(code))
                    {
                        continue;
                    }

                    var name = legendItem.ItemName?.Trim() ?? string.Empty;
                    mergedByCode[code] = new ItemRow
                    {
                        ItemCode = code,
                        ItemName = name
                    };
                }
            }

            if (truthItems != null)
            {
                foreach (var truthItem in truthItems)
                {
                    if (truthItem == null || string.IsNullOrWhiteSpace(truthItem.ItemCode))
                    {
                        continue;
                    }

                    var code = truthItem.ItemCode.Trim().ToUpperInvariant();
                    if (string.IsNullOrWhiteSpace(code))
                    {
                        continue;
                    }

                    var truthName = truthItem.ItemName?.Trim() ?? string.Empty;
                    if (mergedByCode.TryGetValue(code, out var current))
                    {
                        if (string.IsNullOrWhiteSpace(current.ItemName) && !string.IsNullOrWhiteSpace(truthName))
                        {
                            current.ItemName = truthName;
                        }

                        continue;
                    }

                    mergedByCode[code] = new ItemRow
                    {
                        ItemCode = code,
                        ItemName = truthName
                    };
                }
            }

            var ordered = mergedByCode.Values
                .Where(item => !string.IsNullOrWhiteSpace(item.ItemCode))
                .OrderBy(item => item.ItemCode, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var missingNameCount = ordered.Count(item => string.IsNullOrWhiteSpace(item.ItemName));
            if (missingNameCount > 0)
            {
                Debug.LogWarning($"PawOrder import: found {missingNameCount} items without display name after merging sources.");
            }

            return ordered;
        }

        private static List<ItemPromptData> GetDistinctPromptsById(IEnumerable<ItemPromptData> items)
        {
            var result = new List<ItemPromptData>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenRefs = new HashSet<ItemPromptData>();

            foreach (var item in items)
            {
                if (item == null || !seenRefs.Add(item))
                {
                    continue;
                }

                var so = new SerializedObject(item);
                var promptId = so.FindProperty("promptId")?.stringValue?.Trim();
                if (string.IsNullOrWhiteSpace(promptId) || seenIds.Add(promptId))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private static Dictionary<string, QuestionPromptData> EnsureQuestions(List<QuestionRow> questionRows)
        {
            var result = new Dictionary<string, QuestionPromptData>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in questionRows)
            {
                var fileName = $"{row.QuestionId}_{ToSafeSlug(row.QuestionText)}.asset";
                var path = $"{QuestionsDir}/{fileName}";
                var question = AssetDatabase.LoadAssetAtPath<QuestionPromptData>(path);
                if (question == null)
                {
                    question = ScriptableObject.CreateInstance<QuestionPromptData>();
                    AssetDatabase.CreateAsset(question, path);
                }

                var so = new SerializedObject(question);
                so.FindProperty("questionId").stringValue = row.QuestionId;
                so.FindProperty("promptId").stringValue = row.QuestionId;
                so.FindProperty("displayName").stringValue = row.QuestionText;
                so.FindProperty("promptType").enumValueIndex = (int)InvestigationPromptType.Question;
                so.ApplyModifiedPropertiesWithoutUndo();

                result[row.QuestionId] = question;
            }

            return result;
        }

        private static Dictionary<string, ReferencePromptData> EnsureReferencePrompts(
            List<TopicRow> topicRows,
            IReadOnlyDictionary<string, CharacterData> characters,
            IReadOnlyDictionary<string, LocationData> locations,
            IReadOnlyDictionary<string, ItemPromptData> items)
        {
            var result = new Dictionary<string, ReferencePromptData>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in topicRows)
            {
                var classification = ClassifyTopic(row.TopicName);
                if (classification.Category == TopicCategory.FactEvent)
                {
                    continue;
                }

                var promptId = BuildPromptId(classification.Category, row.TopicName);
                var directory = classification.Category switch
                {
                    TopicCategory.Character => ReferenceCharactersDir,
                    TopicCategory.Location => ReferenceLocationsDir,
                    _ => ReferenceLocationsDir
                };

                var path = $"{directory}/{promptId}.asset";
                var prompt = AssetDatabase.LoadAssetAtPath<ReferencePromptData>(path);
                if (prompt == null)
                {
                    prompt = ScriptableObject.CreateInstance<ReferencePromptData>();
                    AssetDatabase.CreateAsset(prompt, path);
                }

                characters.TryGetValue(row.TopicName, out var relatedCharacter);
                locations.TryGetValue(row.TopicName, out var relatedLocation);
                items.TryGetValue(row.TopicName, out var relatedItem);

                var so = new SerializedObject(prompt);
                so.FindProperty("promptId").stringValue = promptId;
                so.FindProperty("displayName").stringValue = row.TopicName;
                so.FindProperty("description").stringValue = row.TopicName;
                so.FindProperty("promptType").enumValueIndex = (int)classification.PromptType;
                so.FindProperty("visibleInPromptLists").boolValue = true;
                so.FindProperty("sourceSheet").stringValue = TopicSheetName;
                so.FindProperty("sourceRow").intValue = row.SourceRow;
                so.FindProperty("topicCategory").stringValue = classification.Category.ToString();
                so.FindProperty("relatedCharacter").objectReferenceValue = relatedCharacter;
                so.FindProperty("relatedLocation").objectReferenceValue = relatedLocation;
                so.FindProperty("relatedItem").objectReferenceValue = relatedItem;
                so.ApplyModifiedPropertiesWithoutUndo();

                result[promptId] = prompt;
            }

            return result;
        }

        private static HashSet<string> CollectUnlockedQuestionIdsFromResponses(List<QuestionRow> rows)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                foreach (var response in row.ResponsesByCharacter.Values)
                {
                    if (string.IsNullOrWhiteSpace(response))
                    {
                        continue;
                    }

                    foreach (Match match in SuperscriptUnlockRegex.Matches(response))
                    {
                        var digits = SuperscriptToDigits(match.Groups[1].Value);
                        if (!string.IsNullOrWhiteSpace(digits))
                        {
                            result.Add("Q" + digits.PadLeft(2, '0'));
                        }
                    }
                }
            }

            return result;
        }

        private static void ApplyStartingQuestions(Dictionary<string, QuestionPromptData> questions, HashSet<string> unlockedQuestionIds)
        {
            var foundAny = false;
            foreach (var pair in questions)
            {
                var isStarting = !unlockedQuestionIds.Contains(pair.Key);
                var so = new SerializedObject(pair.Value);
                so.FindProperty("isStartingQuestion").boolValue = isStarting;
                so.ApplyModifiedPropertiesWithoutUndo();
                foundAny |= isStarting;
            }

            if (!foundAny && questions.TryGetValue("Q01", out var q01))
            {
                var so = new SerializedObject(q01);
                so.FindProperty("isStartingQuestion").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static int CreateInteractionsForQuestions(
            List<QuestionRow> rows,
            IReadOnlyDictionary<string, QuestionPromptData> questions,
            IReadOnlyDictionary<string, ItemPromptData> items,
            IReadOnlyDictionary<string, CharacterData> characters,
            IReadOnlyDictionary<string, LocationData> locations)
        {
            var count = 0;
            foreach (var row in rows)
            {
                if (!questions.TryGetValue(row.QuestionId, out var questionAsset))
                {
                    continue;
                }

                foreach (var responseByCharacter in row.ResponsesByCharacter)
                {
                    if (!TryResolveCharacter(responseByCharacter.Key, characters, out var character))
                    {
                        continue;
                    }

                    var characterSo = new SerializedObject(character);
                    var characterName = characterSo.FindProperty("displayName")?.stringValue;
                    if (string.IsNullOrWhiteSpace(characterName))
                    {
                        characterName = character.name;
                    }

                    var response = responseByCharacter.Value?.Trim();
                    if (string.IsNullOrWhiteSpace(response))
                    {
                        continue;
                    }

                    var referencedPrompts = ResolveReferencedItems(response, items);
                    referencedPrompts.AddRange(ResolveReferencedLocations(row.QuestionText, response, locations));

                    var outcome = CreateOrUpdateOutcome(
                        row.QuestionId,
                        characterName,
                        response,
                        referencedPrompts.Distinct().ToList(),
                        ResolveUnlockQuestions(response, questions));

                    CreateOrUpdateInteraction(
                        row.QuestionId,
                        characterName,
                        character,
                        questionAsset,
                        outcome);

                    count++;
                }
            }

            return count;
        }

        private static int CreateInteractionsForReferencePrompts(
            List<TopicRow> rows,
            IReadOnlyDictionary<string, ReferencePromptData> references,
            IReadOnlyDictionary<string, ItemPromptData> items,
            IReadOnlyDictionary<string, CharacterData> characters)
        {
            var count = 0;
            foreach (var row in rows)
            {
                var promptId = BuildPromptId(ClassifyTopic(row.TopicName).Category, row.TopicName);
                if (!references.TryGetValue(promptId, out var referencePrompt))
                {
                    continue;
                }

                foreach (var responseByCharacter in row.ResponsesByCharacter)
                {
                    if (!TryResolveCharacter(responseByCharacter.Key, characters, out var character))
                    {
                        continue;
                    }

                    var characterSo = new SerializedObject(character);
                    var characterName = characterSo.FindProperty("displayName")?.stringValue;
                    if (string.IsNullOrWhiteSpace(characterName))
                    {
                        characterName = character.name;
                    }

                    var response = responseByCharacter.Value?.Trim();
                    if (string.IsNullOrWhiteSpace(response))
                    {
                        continue;
                    }

                    var outcome = CreateOrUpdateOutcome(
                        promptId,
                        characterName,
                        response,
                        ResolveReferencedItems(response, items),
                        new List<InvestigationPromptData>());

                    CreateOrUpdateInteraction(
                        promptId,
                        characterName,
                        character,
                        referencePrompt,
                        outcome);

                    count++;
                }
            }

            return count;
        }

        private static InteractionOutcomeData CreateOrUpdateOutcome(
            string promptId,
            string characterName,
            string response,
            List<InvestigationPromptData> referencedPrompts,
            List<InvestigationPromptData> unlocksPrompts)
        {
            var dir = $"{OutcomesDir}/{promptId}";
            EnsureDirectory(dir);

            var outcomeId = $"Outcome_{promptId}_{characterName}";
            var path = $"{dir}/{outcomeId}.asset";
            var outcome = AssetDatabase.LoadAssetAtPath<InteractionOutcomeData>(path);
            if (outcome == null)
            {
                outcome = ScriptableObject.CreateInstance<InteractionOutcomeData>();
                AssetDatabase.CreateAsset(outcome, path);
            }

            var so = new SerializedObject(outcome);
            so.FindProperty("outcomeId").stringValue = outcomeId;
            so.FindProperty("responseText").stringValue = response;
            so.FindProperty("proofSummary").stringValue = string.Empty;
            so.FindProperty("createsUsableProof").boolValue = false;
            so.FindProperty("marksPromptAsImportant").boolValue = false;
            SetObjectList(so.FindProperty("referencedPrompts"), referencedPrompts);
            SetObjectList(so.FindProperty("unlocksPrompts"), unlocksPrompts);
            so.FindProperty("unlocksSpecificInteractions").arraySize = 0;
            so.FindProperty("unlocksLocations").arraySize = 0;
            so.FindProperty("suspectEvidenceValues").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            return outcome;
        }

        private static void CreateOrUpdateInteraction(
            string promptId,
            string characterName,
            CharacterData character,
            InvestigationPromptData prompt,
            InteractionOutcomeData outcome)
        {
            var dir = $"{InteractionsDir}/{characterName}";
            EnsureDirectory(dir);

            var interactionId = $"Interaction_{promptId}_{characterName}";
            var path = $"{dir}/{interactionId}.asset";
            var interaction = AssetDatabase.LoadAssetAtPath<CharacterInteractionData>(path);
            if (interaction == null)
            {
                interaction = ScriptableObject.CreateInstance<CharacterInteractionData>();
                AssetDatabase.CreateAsset(interaction, path);
            }

            var so = new SerializedObject(interaction);
            so.FindProperty("interactionId").stringValue = interactionId;
            so.FindProperty("targetCharacter").objectReferenceValue = character;
            so.FindProperty("promptUsed").objectReferenceValue = prompt;
            so.FindProperty("outcome").objectReferenceValue = outcome;
            so.FindProperty("canRepeat").boolValue = true;
            so.FindProperty("consumeInteraction").boolValue = false;

            var requirements = so.FindProperty("requirements");
            requirements.FindPropertyRelative("requiredKnownPrompts").arraySize = 0;
            requirements.FindPropertyRelative("requiredKnownOutcomes").arraySize = 0;
            requirements.FindPropertyRelative("requiredCollectedItems").arraySize = 0;
            requirements.FindPropertyRelative("requiredUnlockedLocations").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateOrUpdateCase(
            Dictionary<string, CharacterData> characters,
            IReadOnlyDictionary<string, LocationData> locations,
            List<InvestigationPromptData> allPrompts,
            List<QuestionPromptData> allQuestions,
            List<ReferencePromptData> allReferences)
        {
            var path = $"{CasesDir}/PawOrder_MainCase.asset";
            var caseData = AssetDatabase.LoadAssetAtPath<CaseData>(path);
            if (caseData == null)
            {
                caseData = ScriptableObject.CreateInstance<CaseData>();
                AssetDatabase.CreateAsset(caseData, path);
            }

            var so = new SerializedObject(caseData);
            so.FindProperty("caseId").stringValue = "PawOrder_MainCase";
            so.FindProperty("caseTitle").stringValue = "Paw & Order";

            SetObjectList(so.FindProperty("allCharacters"), characters.Values.OrderBy(c => c.name).ToList());
            SetObjectList(so.FindProperty("allPrompts"), allPrompts);
            MergeObjectList(so.FindProperty("allLocations"), locations.Values);

            var startingPrompts = new List<InvestigationPromptData>();
            foreach (var question in allQuestions)
            {
                var qso = new SerializedObject(question);
                if (qso.FindProperty("isStartingQuestion").boolValue)
                {
                    startingPrompts.Add(question);
                }
            }

            foreach (var reference in allReferences)
            {
                var rso = new SerializedObject(reference);
                if (rso.FindProperty("startsUnlocked").boolValue)
                {
                    startingPrompts.Add(reference);
                }
            }

            SetObjectList(so.FindProperty("startingPrompts"), startingPrompts);
            var suspects = new List<CharacterData>();
            foreach (var character in characters.Values)
            {
                var cso = new SerializedObject(character);
                if (cso.FindProperty("isSuspect").boolValue)
                {
                    suspects.Add(character);
                }
            }

            SetObjectList(so.FindProperty("suspects"), suspects);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<InvestigationPromptData> ResolveReferencedItems(string response, IReadOnlyDictionary<string, ItemPromptData> items)
        {
            var result = new List<InvestigationPromptData>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in ItemReferenceRegex.Matches(response))
            {
                var code = match.Groups[1].Value.ToUpperInvariant();
                if (!seen.Add(code))
                {
                    continue;
                }

                if (items.TryGetValue(code, out var item))
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private static List<InvestigationPromptData> ResolveReferencedLocations(
            string questionText,
            string response,
            IReadOnlyDictionary<string, LocationData> locations)
        {
            var result = new List<InvestigationPromptData>();
            foreach (var pair in locations)
            {
                var location = pair.Value;
                if (location == null)
                {
                    continue;
                }

                var locationSo = new SerializedObject(location);
                var displayName = locationSo.FindProperty("displayName")?.stringValue;
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                if (!ContainsTerm(questionText, displayName) && !ContainsTerm(response, displayName))
                {
                    continue;
                }

                var promptId = BuildPromptId(TopicCategory.Location, displayName);
                var prompt = AssetDatabase.LoadAssetAtPath<ReferencePromptData>($"{ReferenceLocationsDir}/{promptId}.asset");
                if (prompt != null)
                {
                    result.Add(prompt);
                }
            }

            return result;
        }

        private static bool ContainsTerm(string text, string term)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(term))
            {
                return false;
            }

            return NormalizeLookupKey(text).Contains(NormalizeLookupKey(term), StringComparison.Ordinal);
        }

        private static List<InvestigationPromptData> ResolveUnlockQuestions(string response, IReadOnlyDictionary<string, QuestionPromptData> questions)
        {
            var result = new List<InvestigationPromptData>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in SuperscriptUnlockRegex.Matches(response))
            {
                var digits = SuperscriptToDigits(match.Groups[1].Value);
                if (string.IsNullOrWhiteSpace(digits))
                {
                    continue;
                }

                var key = "Q" + digits.PadLeft(2, '0');
                if (!seen.Add(key))
                {
                    continue;
                }

                if (questions.TryGetValue(key, out var question))
                {
                    result.Add(question);
                }
            }

            return result;
        }

        private static void SetObjectList<T>(SerializedProperty property, IList<T> values) where T : UnityEngine.Object
        {
            property.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void MergeObjectList<T>(SerializedProperty property, IEnumerable<T> valuesToAdd) where T : UnityEngine.Object
        {
            var merged = new List<T>();
            var seen = new HashSet<T>();

            for (var i = 0; i < property.arraySize; i++)
            {
                var existing = property.GetArrayElementAtIndex(i).objectReferenceValue as T;
                if (existing != null && seen.Add(existing))
                {
                    merged.Add(existing);
                }
            }

            foreach (var value in valuesToAdd)
            {
                if (value != null && seen.Add(value))
                {
                    merged.Add(value);
                }
            }

            SetObjectList(property, merged);
        }

        private static (TopicCategory Category, InvestigationPromptType PromptType) ClassifyTopic(string topicName)
        {
            if (CharacterTopics.Contains(topicName))
            {
                return (TopicCategory.Character, InvestigationPromptType.CharacterReference);
            }

            if (LocationTopics.Contains(topicName))
            {
                return (TopicCategory.Location, InvestigationPromptType.LocationReference);
            }

            if (ItemTopics.Contains(topicName))
            {
                return (TopicCategory.ItemOrClue, InvestigationPromptType.Clue);
            }

            return (TopicCategory.FactEvent, InvestigationPromptType.EventReference);
        }

        private static string BuildPromptId(TopicCategory category, string topicName)
        {
            var prefix = category switch
            {
                TopicCategory.Character => "CHR",
                TopicCategory.Location => "LOC",
                TopicCategory.ItemOrClue => "CLUE",
                _ => "FACT"
            };
            return $"{prefix}_{ToSafeSlug(topicName).ToUpperInvariant()}";
        }

        private static string ToSafeSlug(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "ENTRY";
            }

            var normalized = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            }

            var cleaned = Regex.Replace(sb.ToString(), "_+", "_").Trim('_');
            return string.IsNullOrWhiteSpace(cleaned) ? "ENTRY" : cleaned;
        }

        private static string NormalizeLookupKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
                else if (char.IsWhiteSpace(ch) || ch == '_' || ch == '-')
                {
                    sb.Append(' ');
                }
            }

            return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }

        private static string GetFirstTokenKey(string value)
        {
            var normalized = NormalizeLookupKey(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            var separator = normalized.IndexOf(' ');
            return separator < 0 ? normalized : normalized.Substring(0, separator);
        }

        private static string SuperscriptToDigits(string superscript)
        {
            if (string.IsNullOrWhiteSpace(superscript))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(superscript.Length);
            foreach (var ch in superscript)
            {
                sb.Append(ch switch
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
                    _ => '\0'
                });
            }

            return new string(sb.ToString().Where(char.IsDigit).ToArray());
        }

        private static void EnsureDirectoryStructure()
        {
            EnsureDirectory(Root);
            EnsureDirectory(CharactersDir);
            EnsureDirectory(LocationsDir);
            EnsureDirectory(ItemsDir);
            EnsureDirectory(QuestionsDir);
            EnsureDirectory(ReferencesDir);
            EnsureDirectory(ReferenceCharactersDir);
            EnsureDirectory(ReferenceLocationsDir);
            EnsureDirectory(ReferenceFactsDir);
            EnsureDirectory(OutcomesDir);
            EnsureDirectory(InteractionsDir);
            EnsureDirectory(CasesDir);
        }

        private static void EnsureDirectory(string assetPath)
        {
            var parts = assetPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static ParsedWorkbook ParseWorkbook(string xlsxPath)
        {
            using var fileStream = new FileStream(xlsxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);
            XNamespace mainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace pkgRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

            var sharedStrings = ReadSharedStrings(archive, mainNs);
            var workbookDoc = XDocument.Load(archive.GetEntry("xl/workbook.xml")!.Open());
            var workbookRelsDoc = XDocument.Load(archive.GetEntry("xl/_rels/workbook.xml.rels")!.Open());
            var relationships = workbookRelsDoc.Root!
                .Elements(pkgRelNs + "Relationship")
                .ToDictionary(e => (string)e.Attribute("Id")!, e => (string)e.Attribute("Target")!);
            var sheets = workbookDoc.Root!
                .Element(mainNs + "sheets")!
                .Elements(mainNs + "sheet")
                .ToList();

            var topics = new ParsedTopicSheet();
            if (TryGetWorksheetForSheet(archive, sheets, relationships, relNs, TopicSheetName, out var topicWorksheet))
            {
                topics = ParseTopicMatrixSheet(topicWorksheet, sharedStrings, mainNs);
            }
            var questions = ParseQuestionSheets(archive, sheets, relationships, sharedStrings, relNs, mainNs);
            return new ParsedWorkbook
            {
                Topics = topics,
                Questions = questions
            };
        }

        private static bool TryGetWorksheetForSheet(
            ZipArchive archive,
            List<XElement> sheets,
            Dictionary<string, string> relationships,
            XNamespace relNs,
            string sheetName,
            out XDocument worksheet)
        {
            worksheet = null;
            var sheet = sheets.FirstOrDefault(e => string.Equals((string)e.Attribute("name"), sheetName, StringComparison.OrdinalIgnoreCase));
            if (sheet == null)
            {
                return false;
            }

            var relationId = (string)sheet.Attribute(relNs + "id")!;
            var target = relationships[relationId].Replace("\\", "/");
            worksheet = XDocument.Load(archive.GetEntry("xl/" + target)!.Open());
            return true;
        }


        private static XElement GetSheetByName(List<XElement> sheets, string sheetName)
        {
            return sheets.FirstOrDefault(e => string.Equals((string)e.Attribute("name"), sheetName, StringComparison.OrdinalIgnoreCase));
        }

        private static XDocument LoadWorksheetForSheet(
            ZipArchive archive,
            XElement sheet,
            Dictionary<string, string> relationships,
            XNamespace relNs)
        {
            var relationId = (string)sheet.Attribute(relNs + "id")!;
            var target = relationships[relationId].Replace("\\", "/");
            return XDocument.Load(archive.GetEntry("xl/" + target)!.Open());
        }

        private static XDocument GetWorksheetForSheet(
            ZipArchive archive,
            List<XElement> sheets,
            Dictionary<string, string> relationships,
            XNamespace relNs,
            XNamespace mainNs,
            string sheetName)
        {
            var sheet = sheets.FirstOrDefault(e => string.Equals((string)e.Attribute("name"), sheetName, StringComparison.OrdinalIgnoreCase));
            if (sheet == null)
            {
                throw new InvalidOperationException($"PawOrder import failed: sheet '{sheetName}' not found in workbook.");
            }

            var relationId = (string)sheet.Attribute(relNs + "id")!;
            var target = relationships[relationId].Replace("\\", "/");
            return XDocument.Load(archive.GetEntry("xl/" + target)!.Open());
        }

        private static ParsedTopicSheet ParseTopicMatrixSheet(XDocument worksheetDoc, IReadOnlyList<string> sharedStrings, XNamespace mainNs)
        {
            var rows = worksheetDoc.Root!.Element(mainNs + "sheetData")!.Elements(mainNs + "row").ToList();
            var topics = new List<TopicRow>();
            var headerNames = CharacterOrder.ToArray();
            var headerRow = rows.FirstOrDefault(r => (int?)r.Attribute("r") == 3);
            if (headerRow != null)
            {
                headerNames = new[] { "B", "C", "D", "E", "F", "G" }
                    .Select(column => ReadCellText(headerRow, column, sharedStrings, mainNs).Trim())
                    .ToArray();
                headerNames = MergeCharacterHeaderNames(headerNames, CharacterOrder);
            }

            foreach (var row in rows)
            {
                var rowNumber = (int?)row.Attribute("r") ?? 0;
                if (rowNumber <= 3)
                {
                    continue;
                }

                var topicName = ReadCellText(row, "A", sharedStrings, mainNs).Trim();
                if (string.IsNullOrWhiteSpace(topicName))
                {
                    continue;
                }

                topics.Add(new TopicRow
                {
                    SourceRow = rowNumber,
                    TopicName = topicName,
                    ResponsesByCharacter = BuildResponsesByCharacter(row, headerNames, sharedStrings, mainNs)
                });
            }

            Debug.Log($"PawOrder import: sheet '{TopicSheetName}' -> topic rows: {topics.Count}.");
            return new ParsedTopicSheet { Rows = topics };
        }

        private static ParsedQuestionSheet ParseQuestionSheets(
            ZipArchive archive,
            List<XElement> sheets,
            Dictionary<string, string> relationships,
            IReadOnlyList<string> sharedStrings,
            XNamespace relNs,
            XNamespace mainNs)
        {
            var questions = new List<QuestionRow>();
            var items = new List<ItemRow>();

            var sheetOne = GetSheetByName(sheets, TopicSheetName);
            var sheetTwo = GetSheetByName(sheets, QuestionSheetName);

            if (sheetOne != null)
            {
                var worksheetDoc = LoadWorksheetForSheet(archive, sheetOne, relationships, relNs);
                var rows = worksheetDoc.Root!.Element(mainNs + "sheetData")!.Elements(mainNs + "row").ToList();
                var headerNames = CharacterOrder.ToArray();

                var headerRow = rows.FirstOrDefault(r => ((int?)r.Attribute("r") ?? 0) == 1);
                if (headerRow != null)
                {
                    headerNames = ReadCharacterHeaderNames(headerRow, 3, CharacterOrder.Length, CharacterOrder, sharedStrings, mainNs);
                }

                foreach (var row in rows.Where(r => ((int?)r.Attribute("r") ?? 0) >= 2))
                {
                    var questionId = ReadCellText(row, "A", sharedStrings, mainNs).Trim();
                    var questionText = ReadCellText(row, "B", sharedStrings, mainNs).Trim();
                    if (string.IsNullOrWhiteSpace(questionId) || string.IsNullOrWhiteSpace(questionText))
                    {
                        continue;
                    }

                    var responses = new string[CharacterOrder.Length];
                    for (var characterIndex = 0; characterIndex < CharacterOrder.Length; characterIndex++)
                    {
                        var column = ToColumnName(characterIndex + 3);
                        responses[characterIndex] = ReadCellText(row, column, sharedStrings, mainNs).Trim();
                    }

                    questions.Add(new QuestionRow
                    {
                        QuestionId = questionId.ToUpperInvariant(),
                        QuestionText = questionText,
                        ResponsesByCharacter = BuildResponsesByCharacter(responses, headerNames)
                    });
                }
            }

            if (sheetTwo != null)
            {
                var worksheetDoc = LoadWorksheetForSheet(archive, sheetTwo, relationships, relNs);
                var rows = worksheetDoc.Root!.Element(mainNs + "sheetData")!.Elements(mainNs + "row").ToList();
                var headerNames = CharacterOrder.ToArray();

                for (var rowNumber = 3; rowNumber <= 13; rowNumber++)
                {
                    var row = rows.FirstOrDefault(r => ((int?)r.Attribute("r") ?? 0) == rowNumber);
                    if (row == null)
                    {
                        continue;
                    }

                    var itemCode = ReadCellText(row, "J", sharedStrings, mainNs).Trim().ToUpperInvariant();
                    var itemName = ReadCellText(row, "K", sharedStrings, mainNs).Trim();
                    if (!string.IsNullOrWhiteSpace(itemCode) && !string.IsNullOrWhiteSpace(itemName))
                    {
                        items.Add(new ItemRow { ItemCode = itemCode, ItemName = itemName });
                    }
                }

                var headerRow = rows.FirstOrDefault(r => ((int?)r.Attribute("r") ?? 0) == 6);
                if (headerRow != null)
                {
                    headerNames = ReadCharacterHeaderNames(headerRow, 3, CharacterOrder.Length, CharacterOrder, sharedStrings, mainNs);
                }

                foreach (var row in rows.Where(r => ((int?)r.Attribute("r") ?? 0) >= 7))
                {
                    var questionId = ReadCellText(row, "A", sharedStrings, mainNs).Trim();
                    var questionText = ReadCellText(row, "B", sharedStrings, mainNs).Trim();
                    if (string.IsNullOrWhiteSpace(questionId) || string.IsNullOrWhiteSpace(questionText))
                    {
                        continue;
                    }

                    var responses = new string[CharacterOrder.Length];
                    for (var characterIndex = 0; characterIndex < CharacterOrder.Length; characterIndex++)
                    {
                        var column = ToColumnName(characterIndex + 3);
                        responses[characterIndex] = ReadCellText(row, column, sharedStrings, mainNs).Trim();
                    }

                    questions.Add(new QuestionRow
                    {
                        QuestionId = questionId.ToUpperInvariant(),
                        QuestionText = questionText,
                        ResponsesByCharacter = BuildResponsesByCharacter(responses, headerNames)
                    });
                }
            }

            var dedupItems = items
                .Where(i => !string.IsNullOrWhiteSpace(i.ItemCode) && !string.IsNullOrWhiteSpace(i.ItemName))
                .GroupBy(i => i.ItemCode, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(i => i.ItemCode, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var dedupQuestions = questions
                .GroupBy(q => q.QuestionId, StringComparer.OrdinalIgnoreCase)
                .Select(MergeQuestionRows)
                .OrderBy(q => q.QuestionId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Debug.Log($"PawOrder import: parsed {dedupQuestions.Count} questions and {dedupItems.Count} legend items from sheets '{TopicSheetName}' and '{QuestionSheetName}' of '{Path.GetFileName(XlsxPath)}'.");
            return new ParsedQuestionSheet
            {
                Rows = dedupQuestions,
                Items = dedupItems
            };
        }


        private static string[] ReadCharacterHeaderNames(
            XElement headerRow,
            int startColumnIndex,
            int characterCount,
            IReadOnlyList<string> fallbackNames,
            IReadOnlyList<string> sharedStrings,
            XNamespace mainNs)
        {
            var headerNames = new string[characterCount];
            for (var i = 0; i < characterCount; i++)
            {
                var fallbackName = i < fallbackNames.Count ? fallbackNames[i] : string.Empty;
                var headerName = ReadCellText(headerRow, ToColumnName(startColumnIndex + i), sharedStrings, mainNs).Trim();
                headerNames[i] = string.IsNullOrWhiteSpace(headerName) ? fallbackName : headerName;
            }

            return headerNames;
        }

        private static string[] MergeCharacterHeaderNames(IReadOnlyList<string> importedNames, IReadOnlyList<string> fallbackNames)
        {
            var length = Math.Max(importedNames.Count, fallbackNames.Count);
            var headerNames = new string[length];
            for (var i = 0; i < length; i++)
            {
                var importedName = i < importedNames.Count ? importedNames[i]?.Trim() : string.Empty;
                var fallbackName = i < fallbackNames.Count ? fallbackNames[i]?.Trim() : string.Empty;
                headerNames[i] = string.IsNullOrWhiteSpace(importedName) ? fallbackName : importedName;
            }

            return headerNames;
        }

        private static Dictionary<string, string> BuildResponsesByCharacter(
            XElement row,
            IReadOnlyList<string> headerNames,
            IReadOnlyList<string> sharedStrings,
            XNamespace mainNs)
        {
            var responses = new string[headerNames.Count];
            for (var i = 0; i < headerNames.Count; i++)
            {
                var column = ToColumnName(i + 2);
                responses[i] = ReadCellText(row, column, sharedStrings, mainNs).Trim();
            }

            return BuildResponsesByCharacter(responses, headerNames);
        }

        private static Dictionary<string, string> BuildResponsesByCharacter(
            IReadOnlyList<string> responses,
            IReadOnlyList<string> headerNames)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var length = Math.Min(responses.Count, headerNames.Count);
            for (var i = 0; i < length; i++)
            {
                var characterName = headerNames[i]?.Trim();
                var response = responses[i]?.Trim();
                if (string.IsNullOrWhiteSpace(characterName) || string.IsNullOrWhiteSpace(response))
                {
                    continue;
                }

                result[characterName] = response;
            }

            return result;
        }

        private static QuestionRow MergeQuestionRows(IEnumerable<QuestionRow> groupedRows)
        {
            var rows = groupedRows.ToList();
            var first = rows.First();
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                foreach (var pair in row.ResponsesByCharacter)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Value))
                    {
                        merged[pair.Key] = pair.Value;
                    }
                }
            }

            return new QuestionRow
            {
                QuestionId = first.QuestionId,
                QuestionText = first.QuestionText,
                ResponsesByCharacter = merged
            };
        }

        private static bool IsQuestionHeaderRow(string firstColumnValue, IReadOnlyCollection<string> candidateCharacterNames)
        {
            var first = NormalizeLookupKey(firstColumnValue);
            if (first != "pergunta" && first != "perguntas" && first != "interacao" && first != "interacoes")
            {
                return false;
            }

            return candidateCharacterNames.Count >= 2;
        }

        private static IEnumerable<string> EnumerateColumns(int max)
        {
            for (var i = 1; i <= max; i++)
            {
                yield return ToColumnName(i);
            }
        }

        private static string ToColumnName(int index)
        {
            var name = string.Empty;
            var current = index;
            while (current > 0)
            {
                var remainder = (current - 1) % 26;
                name = (char)('A' + remainder) + name;
                current = (current - 1) / 26;
            }

            return name;
        }


        private static string GetColumnNameFromCellReference(string cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                return string.Empty;
            }

            return new string(cellReference.Where(char.IsLetter).ToArray());
        }

        private static string ReadCellText(XElement row, string columnName, IReadOnlyList<string> sharedStrings, XNamespace mainNs)
        {
            var cell = row.Elements(mainNs + "c")
                .FirstOrDefault(c => string.Equals(GetColumnNameFromCellReference((string)c.Attribute("r")), columnName, StringComparison.OrdinalIgnoreCase));
            if (cell == null)
            {
                return string.Empty;
            }

            var cellType = (string)cell.Attribute("t");
            if (cellType == "inlineStr")
            {
                return string.Concat(cell.Descendants(mainNs + "t").Select(t => t.Value));
            }

            var valueNode = cell.Element(mainNs + "v");
            if (valueNode == null)
            {
                return string.Empty;
            }

            var raw = valueNode.Value;
            if (cellType == "s" && int.TryParse(raw, out var index) && index >= 0 && index < sharedStrings.Count)
            {
                return sharedStrings[index];
            }

            return raw;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive, XNamespace mainNs)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return new List<string>();
            }

            var doc = XDocument.Load(entry.Open());
            return doc.Root!
                .Elements(mainNs + "si")
                .Select(si => string.Concat(si.Descendants(mainNs + "t").Select(t => t.Value)))
                .ToList();
        }

        private enum TopicCategory
        {
            Character,
            Location,
            ItemOrClue,
            FactEvent
        }

        private sealed class ParsedWorkbook
        {
            public ParsedTopicSheet Topics = new();
            public ParsedQuestionSheet Questions = new();
        }

        private sealed class TruthWorkbookImportData
        {
            public List<string> CharacterNames { get; } = new();
            public List<ItemRow> ItemRows { get; } = new();
            public List<TruthLocationImportEntry> LocationEntries { get; } = new();
        }

        private sealed class ParsedTopicSheet
        {
            public List<TopicRow> Rows = new();
        }

        private sealed class ParsedQuestionSheet
        {
            public List<QuestionRow> Rows = new();
            public List<ItemRow> Items = new();
        }

        private sealed class TopicRow
        {
            public int SourceRow;
            public string TopicName = string.Empty;
            public Dictionary<string, string> ResponsesByCharacter = new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class ItemRow
        {
            public string ItemCode = string.Empty;
            public string ItemName = string.Empty;
        }

        private sealed class QuestionRow
        {
            public string QuestionId = string.Empty;
            public string QuestionText = string.Empty;
            public Dictionary<string, string> ResponsesByCharacter = new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class TruthLocationImportEntry
        {
            public TruthLocationImportEntry(string locationName)
            {
                LocationName = locationName;
            }

            public string LocationName { get; }
            public HashSet<string> CharacterNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> ItemNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}


