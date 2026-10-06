using System;
using System.Collections.Generic;
using System.Linq;

namespace Fabula.PawOrder.Editor
{
    internal sealed class PawOrderTruthDatabase
    {
        #region Fields

        private readonly Dictionary<string, PawOrderTruthCharacterProfile> profilesByKey = new Dictionary<string, PawOrderTruthCharacterProfile>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Properties

        public IReadOnlyCollection<PawOrderTruthCharacterProfile> Profiles => profilesByKey.Values.Distinct().ToList();

        #endregion

        #region Public API

        public static PawOrderTruthDatabase FromWorkbook(PawOrderExcelWorkbookReader workbook)
        {
            PawOrderTruthDatabase database = new PawOrderTruthDatabase();
            database.ImportCharacterSummary(workbook);
            database.ImportOverviewAccessFlags(workbook);
            return database;
        }

        public bool TryFindProfile(CharacterData character, out PawOrderTruthCharacterProfile profile)
        {
            profile = null;
            if (character == null)
            {
                return false;
            }

            return TryFindProfile(character.DisplayName, out profile) || TryFindProfile(character.CharacterId, out profile) || TryFindProfile(character.name, out profile);
        }

        public bool TryFindProfile(string characterName, out PawOrderTruthCharacterProfile profile)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return false;
            }

            string key = NormalizeKey(characterName);
            if (profilesByKey.TryGetValue(key, out profile))
            {
                return true;
            }

            string firstToken = GetFirstToken(key);
            return profilesByKey.TryGetValue(firstToken, out profile);
        }

        #endregion

        #region Internal Logic

        private void ImportCharacterSummary(PawOrderExcelWorkbookReader workbook)
        {
            if (!workbook.TryGetSheet("Resumo Personagens", out List<List<string>> rows))
            {
                return;
            }

            int headerRowIndex = FindRowIndex(rows, "Personagem", "Papel no caso");
            if (headerRowIndex < 0)
            {
                return;
            }

            Dictionary<string, int> columns = BuildColumnMap(rows[headerRowIndex]);
            for (int rowIndex = headerRowIndex + 1; rowIndex < rows.Count; rowIndex++)
            {
                List<string> row = rows[rowIndex];
                string characterName = GetCell(row, columns, "Personagem");
                if (string.IsNullOrWhiteSpace(characterName))
                {
                    continue;
                }

                PawOrderTruthCharacterProfile profile = GetOrCreateProfile(characterName);
                profile.RoleInCase = GetCell(row, columns, "Papel no caso");
                profile.BehaviorVoice = GetCell(row, columns, "Comportamento / voz");
                profile.FinalLocation = GetCell(row, columns, "Local final");
                profile.DocumentedPath = GetCell(row, columns, "Trajeto real documentado");
                profile.DirectContactItems = GetCell(row, columns, "Itens com contato direto");
                profile.UsedOrManipulatedItems = GetCell(row, columns, "Itens usados ou manipulados");
                profile.DirectlyLinkedCharacters = GetCell(row, columns, "Outros personagens ligados diretamente");
                profile.PlausibleLieOrOmission = GetCell(row, columns, "Mentira / omissão plausível");
                profile.RealAction = GetCell(row, columns, "O que realmente fez");
                profile.IsCanonicalCulprit = ContainsAny(profile.RoleInCase, "culpada real", "culpado real", "culpade real");
                profile.HasMeansSupport = HasMeaningfulText(profile.DirectContactItems) || HasMeaningfulText(profile.UsedOrManipulatedItems);
                profile.HasMotivationSupport = HasMeaningfulText(profile.RoleInCase) || HasMeaningfulText(profile.PlausibleLieOrOmission);
            }
        }

        private void ImportOverviewAccessFlags(PawOrderExcelWorkbookReader workbook)
        {
            if (!workbook.TryGetSheet("Visao Geral", out List<List<string>> rows))
            {
                return;
            }

            int headerRowIndex = FindRowIndex(rows, "Personagem", "Suspeito com acesso ao camarim?");
            if (headerRowIndex < 0)
            {
                return;
            }

            Dictionary<string, int> columns = BuildColumnMap(rows[headerRowIndex]);
            for (int rowIndex = headerRowIndex + 1; rowIndex < rows.Count; rowIndex++)
            {
                List<string> row = rows[rowIndex];
                string characterName = GetCell(row, columns, "Personagem");
                if (string.IsNullOrWhiteSpace(characterName))
                {
                    continue;
                }

                PawOrderTruthCharacterProfile profile = GetOrCreateProfile(characterName);
                string accessValue = GetCell(row, columns, "Suspeito com acesso ao camarim?");
                profile.HasDressingRoomAccess = IsYes(accessValue);
                profile.HasOpportunitySupport = profile.HasDressingRoomAccess || ContainsAny(profile.DocumentedPath, "camarim");
            }
        }

        private PawOrderTruthCharacterProfile GetOrCreateProfile(string characterName)
        {
            string key = NormalizeKey(characterName);
            if (profilesByKey.TryGetValue(key, out PawOrderTruthCharacterProfile existingProfile))
            {
                return existingProfile;
            }

            PawOrderTruthCharacterProfile profile = new PawOrderTruthCharacterProfile(characterName);
            profilesByKey[key] = profile;
            string firstToken = GetFirstToken(key);
            if (!profilesByKey.ContainsKey(firstToken))
            {
                profilesByKey[firstToken] = profile;
            }

            return profile;
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

        private static string GetCell(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> columns, string columnName)
        {
            if (!columns.TryGetValue(columnName, out int columnIndex) || columnIndex < 0 || columnIndex >= row.Count)
            {
                return string.Empty;
            }

            return row[columnIndex]?.Trim() ?? string.Empty;
        }

        private static bool IsYes(string value)
        {
            return string.Equals(value?.Trim(), "Sim", StringComparison.OrdinalIgnoreCase) || string.Equals(value?.Trim(), "Yes", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasMeaningfulText(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Trim().Length > 8;
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return needles.Any(needle => value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string NormalizeKey(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
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

    internal sealed class PawOrderTruthCharacterProfile
    {
        #region Constructors

        public PawOrderTruthCharacterProfile(string characterName)
        {
            CharacterName = characterName;
        }

        #endregion

        #region Properties

        public string CharacterName { get; }
        public string RoleInCase { get; set; }
        public string BehaviorVoice { get; set; }
        public string FinalLocation { get; set; }
        public string DocumentedPath { get; set; }
        public string DirectContactItems { get; set; }
        public string UsedOrManipulatedItems { get; set; }
        public string DirectlyLinkedCharacters { get; set; }
        public string PlausibleLieOrOmission { get; set; }
        public string RealAction { get; set; }
        public bool HasDressingRoomAccess { get; set; }
        public bool HasMotivationSupport { get; set; }
        public bool HasMeansSupport { get; set; }
        public bool HasOpportunitySupport { get; set; }
        public bool IsCanonicalCulprit { get; set; }

        #endregion
    }
}
