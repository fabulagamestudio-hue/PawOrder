using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Fabula.PawOrder.Editor
{
    internal static class PawOrderEvidenceContextImporter
    {
        #region Fields

        internal const string TimelineRelationship = "Timeline";
        internal const string ItemContextRelationship = "Item Context";
        internal const string DirectContactRelationship = "Direct Contact";
        internal const string ProximityContactRelationship = "Proximity Contact";

        private const string TimelineSheetName = "Timeline";
        private const string ItemsSheetName = "Itens";
        private const string OverviewSheetName = "Visao Geral";
        private const string SummarySheetName = "Resumo Personagens";

        #endregion

        #region Public API

        internal static PawOrderEvidenceContextDatabase ImportFromWorkbook(string workbookPath, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(workbookPath) || !File.Exists(workbookPath))
            {
                throw new FileNotFoundException("Truth workbook was not found.", workbookPath);
            }

            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Context database asset path must be inside the Assets folder.", nameof(assetPath));
            }

            PawOrderExcelWorkbookReader workbook = PawOrderExcelWorkbookReader.Load(workbookPath);
            PawOrderEvidenceContextImportBuilder builder = new PawOrderEvidenceContextImportBuilder();
            ImportTimeline(workbook, builder);
            ImportItems(workbook, builder);
            ImportOverview(workbook, builder);
            ImportSummary(workbook, builder);

            string directory = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrWhiteSpace(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                CreateFolders(directory);
            }

            PawOrderEvidenceContextDatabase database = AssetDatabase.LoadAssetAtPath<PawOrderEvidenceContextDatabase>(assetPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<PawOrderEvidenceContextDatabase>();
                AssetDatabase.CreateAsset(database, assetPath);
            }

            database.ReplaceContent(
                builder.BuildCharacters(),
                builder.BuildLocations(),
                builder.BuildItems(),
                builder.LocationLinks,
                builder.ItemLinks);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return database;
        }

        #endregion

        #region Internal Logic

        private static void ImportTimeline(PawOrderExcelWorkbookReader workbook, PawOrderEvidenceContextImportBuilder builder)
        {
            if (!workbook.TryGetSheet(TimelineSheetName, out List<List<string>> rows))
            {
                return;
            }

            int headerRowIndex = FindRowIndex(rows, "Horário");
            if (headerRowIndex < 0)
            {
                return;
            }

            List<string> headerRow = rows[headerRowIndex];
            int timeColumn = FindColumn(headerRow, "Horário");
            int readingColumn = FindColumn(headerRow, "Leitura do momento");
            int finalColumn = FindColumn(headerRow, "Localização final derivada?");

            for (int columnIndex = 0; columnIndex < headerRow.Count; columnIndex++)
            {
                if (columnIndex == timeColumn || columnIndex == readingColumn || columnIndex == finalColumn)
                {
                    continue;
                }

                string characterName = headerRow[columnIndex]?.Trim();
                if (string.IsNullOrWhiteSpace(characterName))
                {
                    continue;
                }

                builder.AddCharacter(characterName);
                for (int rowIndex = headerRowIndex + 1; rowIndex < rows.Count; rowIndex++)
                {
                    string timeValue = ReadCell(rows[rowIndex], timeColumn);
                    string locationName = CleanLocationName(ReadCell(rows[rowIndex], columnIndex));
                    if (string.IsNullOrWhiteSpace(locationName))
                    {
                        continue;
                    }

                    builder.AddLocation(locationName);
                    builder.AddLocationLink(characterName, locationName, TimelineRelationship, TimelineSheetName + " " + timeValue);
                }
            }
        }

        private static void ImportItems(PawOrderExcelWorkbookReader workbook, PawOrderEvidenceContextImportBuilder builder)
        {
            if (!workbook.TryGetSheet(ItemsSheetName, out List<List<string>> rows))
            {
                return;
            }

            int headerRowIndex = FindRowIndex(rows, "Item", "Cenário", "Contato direto", "Contato por proximidade");
            if (headerRowIndex < 0)
            {
                return;
            }

            Dictionary<string, int> columns = BuildColumnMap(rows[headerRowIndex]);
            for (int rowIndex = headerRowIndex + 1; rowIndex < rows.Count; rowIndex++)
            {
                List<string> row = rows[rowIndex];
                string itemName = ReadCell(row, columns, "Item");
                string locationName = CleanLocationName(ReadCell(row, columns, "Cenário"));
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    continue;
                }

                builder.AddItem(itemName);
                if (!string.IsNullOrWhiteSpace(locationName))
                {
                    builder.AddLocation(locationName);
                }

                AddItemLinks(builder, itemName, locationName, ReadCell(row, columns, "Contato direto"), DirectContactRelationship);
                AddItemLinks(builder, itemName, locationName, ReadCell(row, columns, "Contato por proximidade"), ProximityContactRelationship);
            }
        }

        private static void ImportOverview(PawOrderExcelWorkbookReader workbook, PawOrderEvidenceContextImportBuilder builder)
        {
            if (!workbook.TryGetSheet(OverviewSheetName, out List<List<string>> rows))
            {
                return;
            }

            int headerRowIndex = FindRowIndex(rows, "Personagem", "Locais visitados");
            if (headerRowIndex < 0)
            {
                return;
            }

            Dictionary<string, int> columns = BuildColumnMap(rows[headerRowIndex]);
            for (int rowIndex = headerRowIndex + 1; rowIndex < rows.Count; rowIndex++)
            {
                List<string> row = rows[rowIndex];
                string characterName = ReadCell(row, columns, "Personagem");
                if (string.IsNullOrWhiteSpace(characterName))
                {
                    continue;
                }

                builder.AddCharacter(characterName);
                foreach (string locationName in SplitList(ReadCell(row, columns, "Locais visitados")).Select(CleanLocationName))
                {
                    if (string.IsNullOrWhiteSpace(locationName))
                    {
                        continue;
                    }

                    builder.AddLocation(locationName);
                    builder.AddLocationLink(characterName, locationName, TimelineRelationship, OverviewSheetName);
                }
            }
        }

        private static void ImportSummary(PawOrderExcelWorkbookReader workbook, PawOrderEvidenceContextImportBuilder builder)
        {
            if (!workbook.TryGetSheet(SummarySheetName, out List<List<string>> rows))
            {
                return;
            }

            int headerRowIndex = FindRowIndex(rows, "Personagem", "Trajeto real documentado");
            if (headerRowIndex < 0)
            {
                return;
            }

            Dictionary<string, int> columns = BuildColumnMap(rows[headerRowIndex]);
            for (int rowIndex = headerRowIndex + 1; rowIndex < rows.Count; rowIndex++)
            {
                List<string> row = rows[rowIndex];
                string characterName = ReadCell(row, columns, "Personagem");
                if (string.IsNullOrWhiteSpace(characterName))
                {
                    continue;
                }

                builder.AddCharacter(characterName);
                ImportLocationsFromPath(builder, characterName, ReadCell(row, columns, "Trajeto real documentado"));
                AddSummaryItems(builder, characterName, ReadCell(row, columns, "Itens com contato direto"), DirectContactRelationship);
                AddSummaryItems(builder, characterName, ReadCell(row, columns, "Itens usados ou manipulados"), DirectContactRelationship);
            }
        }

        private static void AddItemLinks(PawOrderEvidenceContextImportBuilder builder, string itemName, string locationName, string charactersText, string relationshipType)
        {
            foreach (string characterName in SplitList(charactersText))
            {
                builder.AddCharacter(characterName);
                builder.AddItemLink(characterName, itemName, locationName, relationshipType, ItemsSheetName);
                if (!string.IsNullOrWhiteSpace(locationName))
                {
                    builder.AddLocationLink(characterName, locationName, ItemContextRelationship, ItemsSheetName + " " + itemName);
                }
            }
        }

        private static void AddSummaryItems(PawOrderEvidenceContextImportBuilder builder, string characterName, string itemsText, string relationshipType)
        {
            foreach (string itemName in SplitList(itemsText))
            {
                builder.AddItem(itemName);
                builder.AddItemLink(characterName, itemName, string.Empty, relationshipType, SummarySheetName);
            }
        }

        private static void ImportLocationsFromPath(PawOrderEvidenceContextImportBuilder builder, string characterName, string pathText)
        {
            foreach (string segment in SplitPathSegments(pathText))
            {
                string locationName = ExtractLocationFromPathSegment(segment);
                if (string.IsNullOrWhiteSpace(locationName))
                {
                    continue;
                }

                builder.AddLocation(locationName);
                builder.AddLocationLink(characterName, locationName, TimelineRelationship, SummarySheetName);
            }
        }

        private static IEnumerable<string> SplitList(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            string normalized = value.Replace(";", ",");
            string[] parts = normalized.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string clean = part.Trim();
                if (string.IsNullOrWhiteSpace(clean))
                {
                    continue;
                }

                yield return clean;
            }
        }

        private static IEnumerable<string> SplitPathSegments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            string[] parts = value.Split(new[] { "->" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string clean = part.Trim();
                if (!string.IsNullOrWhiteSpace(clean))
                {
                    yield return clean;
                }
            }
        }

        private static string ExtractLocationFromPathSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return string.Empty;
            }

            int parenthesisIndex = segment.IndexOf('(');
            string clean = parenthesisIndex > 0 ? segment.Substring(0, parenthesisIndex) : segment;
            clean = clean.Replace("/ chamada", string.Empty).Replace("pós-choque", string.Empty).Trim();
            return CleanLocationName(clean);
        }

        private static string CleanLocationName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string clean = value.Trim();
            int parenthesisIndex = clean.IndexOf('(');
            if (parenthesisIndex > 0)
            {
                clean = clean.Substring(0, parenthesisIndex).Trim();
            }

            return clean.Trim('.', ';', ',');
        }

        private static int FindRowIndex(IReadOnlyList<List<string>> rows, params string[] requiredHeaders)
        {
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                string combined = string.Join("|", rows[rowIndex]);
                bool containsAll = requiredHeaders.All(header => combined.IndexOf(header, StringComparison.OrdinalIgnoreCase) >= 0);
                if (containsAll)
                {
                    return rowIndex;
                }
            }

            return -1;
        }

        private static Dictionary<string, int> BuildColumnMap(IReadOnlyList<string> row)
        {
            Dictionary<string, int> columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                string value = row[columnIndex];
                if (!string.IsNullOrWhiteSpace(value) && !columns.ContainsKey(value))
                {
                    columns[value] = columnIndex;
                }
            }

            return columns;
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

        private static string ReadCell(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> columns, string columnName)
        {
            if (!columns.TryGetValue(columnName, out int columnIndex))
            {
                return string.Empty;
            }

            return ReadCell(row, columnIndex);
        }

        private static string ReadCell(IReadOnlyList<string> row, int columnIndex)
        {
            if (row == null || columnIndex < 0 || columnIndex >= row.Count)
            {
                return string.Empty;
            }

            return row[columnIndex]?.Trim() ?? string.Empty;
        }

        private static void CreateFolders(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string currentPath = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string nextPath = currentPath + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, parts[index]);
                }

                currentPath = nextPath;
            }
        }

        #endregion
    }

    internal sealed class PawOrderEvidenceContextImportBuilder
    {
        #region Fields

        private readonly Dictionary<string, HashSet<string>> characterAliases = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> locationAliases = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> itemAliases = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> locationLinkKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> itemLinkKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Properties

        internal List<PawOrderEvidenceContextLocationLink> LocationLinks { get; } = new List<PawOrderEvidenceContextLocationLink>();
        internal List<PawOrderEvidenceContextItemLink> ItemLinks { get; } = new List<PawOrderEvidenceContextItemLink>();

        #endregion

        #region Public API

        internal void AddCharacter(string characterName)
        {
            string canonical = Canonicalize(characterName);
            if (string.IsNullOrWhiteSpace(canonical))
            {
                return;
            }

            if (!characterAliases.TryGetValue(canonical, out HashSet<string> aliases))
            {
                aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                characterAliases[canonical] = aliases;
            }

            aliases.Add(canonical);
            string firstToken = canonical.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstToken))
            {
                aliases.Add(firstToken);
            }
        }

        internal void AddLocation(string locationName)
        {
            string canonical = Canonicalize(locationName);
            if (string.IsNullOrWhiteSpace(canonical))
            {
                return;
            }

            if (!locationAliases.TryGetValue(canonical, out HashSet<string> aliases))
            {
                aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                locationAliases[canonical] = aliases;
            }

            aliases.Add(canonical);
            AddLocationVariants(canonical, aliases);
        }

        internal void AddItem(string itemName)
        {
            string canonical = Canonicalize(itemName);
            if (string.IsNullOrWhiteSpace(canonical))
            {
                return;
            }

            if (!itemAliases.TryGetValue(canonical, out HashSet<string> aliases))
            {
                aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                itemAliases[canonical] = aliases;
            }

            aliases.Add(canonical);
            AddItemVariants(canonical, aliases);
        }

        internal void AddLocationLink(string characterName, string locationName, string relationshipType, string source)
        {
            string character = Canonicalize(characterName);
            string location = Canonicalize(locationName);
            if (string.IsNullOrWhiteSpace(character) || string.IsNullOrWhiteSpace(location))
            {
                return;
            }

            AddCharacter(character);
            AddLocation(location);
            string key = character + "|" + location + "|" + relationshipType;
            if (!locationLinkKeys.Add(key))
            {
                return;
            }

            LocationLinks.Add(new PawOrderEvidenceContextLocationLink(character, location, relationshipType, source));
        }

        internal void AddItemLink(string characterName, string itemName, string locationName, string relationshipType, string source)
        {
            string character = Canonicalize(characterName);
            string item = Canonicalize(itemName);
            string location = Canonicalize(locationName);
            if (string.IsNullOrWhiteSpace(character) || string.IsNullOrWhiteSpace(item))
            {
                return;
            }

            AddCharacter(character);
            AddItem(item);
            string key = character + "|" + item + "|" + relationshipType;
            if (!itemLinkKeys.Add(key))
            {
                return;
            }

            ItemLinks.Add(new PawOrderEvidenceContextItemLink(character, item, location, relationshipType, source));
        }

        internal List<PawOrderEvidenceContextCharacter> BuildCharacters()
        {
            return characterAliases
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new PawOrderEvidenceContextCharacter(pair.Key, pair.Value))
                .ToList();
        }

        internal List<PawOrderEvidenceContextLocation> BuildLocations()
        {
            return locationAliases
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new PawOrderEvidenceContextLocation(pair.Key, pair.Value))
                .ToList();
        }

        internal List<PawOrderEvidenceContextItem> BuildItems()
        {
            return itemAliases
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new PawOrderEvidenceContextItem(pair.Key, pair.Value))
                .ToList();
        }

        #endregion

        #region Internal Logic

        private static string Canonicalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string clean = value.Trim();
            clean = clean.Trim('.', ';', ',');
            return clean;
        }

        private static void AddLocationVariants(string value, HashSet<string> aliases)
        {
            if (value.IndexOf(" de ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                aliases.Add(value.Replace(" de ", " "));
            }

            if (value.IndexOf(" / ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                foreach (string part in value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string clean = part.Trim();
                    if (!string.IsNullOrWhiteSpace(clean))
                    {
                        aliases.Add(clean);
                    }
                }
            }
        }

        private static void AddItemVariants(string value, HashSet<string> aliases)
        {
            string[] markers = { " com ", " de ", " aberto", " aberta", " caro", " cara", " amassada", " quebrado", " quebrada" };
            foreach (string marker in markers)
            {
                int markerIndex = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex > 2)
                {
                    aliases.Add(value.Substring(0, markerIndex).Trim());
                }
            }
        }

        #endregion
    }
}
