using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Fabula.PawOrder.Editor
{
    internal sealed class PawOrderExcelWorkbookReader
    {
        #region Fields

        private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

        private readonly Dictionary<string, List<List<string>>> sheetsByName = new Dictionary<string, List<List<string>>>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Properties

        public IReadOnlyDictionary<string, List<List<string>>> SheetsByName => sheetsByName;

        #endregion

        #region Public API

        public static PawOrderExcelWorkbookReader Load(string workbookPath)
        {
            if (string.IsNullOrWhiteSpace(workbookPath))
            {
                throw new ArgumentException("Workbook path is empty.", nameof(workbookPath));
            }

            if (!File.Exists(workbookPath))
            {
                throw new FileNotFoundException("Workbook file was not found.", workbookPath);
            }

            PawOrderExcelWorkbookReader reader = new PawOrderExcelWorkbookReader();
            reader.ReadWorkbook(workbookPath);
            return reader;
        }

        public bool TryGetSheet(string sheetName, out List<List<string>> rows)
        {
            return sheetsByName.TryGetValue(sheetName, out rows);
        }

        #endregion

        #region Internal Logic

        private void ReadWorkbook(string workbookPath)
        {
            using (ZipArchive archive = ZipFile.OpenRead(workbookPath))
            {
                List<string> sharedStrings = ReadSharedStrings(archive);
                Dictionary<string, string> relationshipTargets = ReadWorkbookRelationships(archive);
                List<SheetReference> sheetReferences = ReadSheetReferences(archive, relationshipTargets);

                foreach (SheetReference sheetReference in sheetReferences)
                {
                    ZipArchiveEntry sheetEntry = archive.GetEntry(sheetReference.TargetPath);
                    if (sheetEntry == null)
                    {
                        continue;
                    }

                    sheetsByName[sheetReference.Name] = ReadSheet(sheetEntry, sharedStrings);
                }
            }
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            ZipArchiveEntry sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (sharedStringsEntry == null)
            {
                return new List<string>();
            }

            XDocument document = LoadXml(sharedStringsEntry);
            return document
                .Descendants(SpreadsheetNamespace + "si")
                .Select(ReadSharedString)
                .ToList();
        }

        private static string ReadSharedString(XElement sharedStringElement)
        {
            IEnumerable<XElement> textNodes = sharedStringElement.Descendants(SpreadsheetNamespace + "t");
            return string.Concat(textNodes.Select(text => text.Value));
        }

        private static Dictionary<string, string> ReadWorkbookRelationships(ZipArchive archive)
        {
            ZipArchiveEntry relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
            if (relationshipsEntry == null)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            XDocument document = LoadXml(relationshipsEntry);
            Dictionary<string, string> relationships = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (XElement relationship in document.Descendants(PackageRelationshipsNamespace + "Relationship"))
            {
                string id = relationship.Attribute("Id")?.Value;
                string target = relationship.Attribute("Target")?.Value;
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                relationships[id] = NormalizeWorkbookTarget(target);
            }

            return relationships;
        }

        private static List<SheetReference> ReadSheetReferences(ZipArchive archive, Dictionary<string, string> relationshipTargets)
        {
            ZipArchiveEntry workbookEntry = archive.GetEntry("xl/workbook.xml");
            if (workbookEntry == null)
            {
                return new List<SheetReference>();
            }

            XDocument document = LoadXml(workbookEntry);
            List<SheetReference> references = new List<SheetReference>();
            foreach (XElement sheetElement in document.Descendants(SpreadsheetNamespace + "sheet"))
            {
                string name = sheetElement.Attribute("name")?.Value;
                string relationshipId = sheetElement.Attribute(RelationshipsNamespace + "id")?.Value;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(relationshipId))
                {
                    continue;
                }

                if (!relationshipTargets.TryGetValue(relationshipId, out string targetPath))
                {
                    continue;
                }

                references.Add(new SheetReference(name, targetPath));
            }

            return references;
        }

        private static List<List<string>> ReadSheet(ZipArchiveEntry sheetEntry, IReadOnlyList<string> sharedStrings)
        {
            XDocument document = LoadXml(sheetEntry);
            Dictionary<int, Dictionary<int, string>> rowsByIndex = new Dictionary<int, Dictionary<int, string>>();
            int maxColumnIndex = 0;

            foreach (XElement rowElement in document.Descendants(SpreadsheetNamespace + "row"))
            {
                int rowIndex = ReadInt(rowElement.Attribute("r")?.Value, rowsByIndex.Count + 1);
                Dictionary<int, string> cellValues = new Dictionary<int, string>();

                foreach (XElement cellElement in rowElement.Elements(SpreadsheetNamespace + "c"))
                {
                    string reference = cellElement.Attribute("r")?.Value;
                    int columnIndex = GetColumnIndex(reference);
                    if (columnIndex <= 0)
                    {
                        columnIndex = cellValues.Count + 1;
                    }

                    maxColumnIndex = Math.Max(maxColumnIndex, columnIndex);
                    cellValues[columnIndex] = ReadCellValue(cellElement, sharedStrings);
                }

                rowsByIndex[rowIndex] = cellValues;
            }

            int maxRowIndex = rowsByIndex.Count == 0 ? 0 : rowsByIndex.Keys.Max();
            List<List<string>> rows = new List<List<string>>();
            for (int rowIndex = 1; rowIndex <= maxRowIndex; rowIndex++)
            {
                List<string> row = new List<string>();
                rowsByIndex.TryGetValue(rowIndex, out Dictionary<int, string> sourceRow);
                for (int columnIndex = 1; columnIndex <= maxColumnIndex; columnIndex++)
                {
                    string value = string.Empty;
                    if (sourceRow != null && sourceRow.TryGetValue(columnIndex, out string sourceValue))
                    {
                        value = sourceValue ?? string.Empty;
                    }

                    row.Add(value);
                }

                rows.Add(row);
            }

            return rows;
        }

        private static string ReadCellValue(XElement cellElement, IReadOnlyList<string> sharedStrings)
        {
            string cellType = cellElement.Attribute("t")?.Value;
            if (string.Equals(cellType, "inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat(cellElement.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value));
            }

            string rawValue = cellElement.Element(SpreadsheetNamespace + "v")?.Value ?? string.Empty;
            if (string.Equals(cellType, "s", StringComparison.OrdinalIgnoreCase))
            {
                int sharedStringIndex = ReadInt(rawValue, -1);
                if (sharedStringIndex >= 0 && sharedStringIndex < sharedStrings.Count)
                {
                    return sharedStrings[sharedStringIndex];
                }
            }

            return rawValue;
        }

        private static XDocument LoadXml(ZipArchiveEntry entry)
        {
            using (Stream stream = entry.Open())
            {
                return XDocument.Load(stream);
            }
        }

        private static string NormalizeWorkbookTarget(string target)
        {
            string normalized = target.Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = normalized.TrimStart('/');
            }

            if (!normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "xl/" + normalized;
            }

            return normalized;
        }

        private static int ReadInt(string value, int fallback)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : fallback;
        }

        private static int GetColumnIndex(string cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                return 0;
            }

            Match match = Regex.Match(cellReference, "^[A-Za-z]+");
            if (!match.Success)
            {
                return 0;
            }

            int columnIndex = 0;
            foreach (char letter in match.Value.ToUpperInvariant())
            {
                columnIndex *= 26;
                columnIndex += letter - 'A' + 1;
            }

            return columnIndex;
        }

        #endregion

        #region Nested Types

        private readonly struct SheetReference
        {
            public SheetReference(string name, string targetPath)
            {
                Name = name;
                TargetPath = targetPath;
            }

            public string Name { get; }
            public string TargetPath { get; }
        }

        #endregion
    }
}
