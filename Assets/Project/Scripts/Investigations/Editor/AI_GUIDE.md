# Mapa — Investigations / Editor

- **Caminho:** `Assets/Project/Scripts/Investigations/Editor/`
- **Namespace:** `Fabula.PawOrder.Editor`
- **Propósito:** ferramentas de autoria do caso — importação das planilhas, pontuação e validação de evidências, organização de menus e wizards de cena.
- **Guia:** [InvestigationEditorToolsGuide.md](InvestigationEditorToolsGuide.md) · sistema: [InvestigationSystemGuide.md](../InvestigationSystemGuide.md)

## Importação de conteúdo

| Arquivo | Classe | Responsabilidade | API / menu |
|---|---|---|---|
| `PawOrderCaseBootstrapImporter.cs` | `PawOrderCaseBootstrapImporter` (static) | Gera os assets do caso a partir das planilhas | `ImportFromSpreadsheet` · `Fabula/Paw Order/Investigations/Import Paw & Order Case Assets` |
| `PawOrderQuestionUpdateWindow.cs` | `PawOrderQuestionUpdateWindow` | Janela para adicionar perguntas novas | `Fabula/Paw Order/Investigations/Update New Questions` |
| `PawOrderQuestionUpdateService.cs` | `PawOrderQuestionUpdateService` | Cria perguntas, outcomes e interações faltantes | `PreviewUpdate`, `ApplyUpdate` |
| `PawOrderQuestionUpdateReport.cs` | `PawOrderQuestionUpdateReport` | Resultado da atualização de perguntas | `AddMessage`, `AddWarning`, `Register*` |
| `PawOrderExcelWorkbookReader.cs` | `PawOrderExcelWorkbookReader` | Leitor de `.xlsx` | `Load`, `TryGetSheet` |
| `PawOrderQuestionnaireDatabase.cs` | `PawOrderQuestionnaireDatabase`, `PawOrderQuestionnaireQuestion` | Perguntas e respostas lidas da planilha | `FromWorkbook`, `ContainsQuestion` |
| `PawOrderTruthDatabase.cs` | `PawOrderTruthDatabase`, `PawOrderTruthCharacterProfile` | Verdade canônica do mistério lida da planilha | `FromWorkbook`, `TryFindProfile` |

## Evidências (pontuação por suspeito)

| Arquivo | Classe | Responsabilidade | API / menu |
|---|---|---|---|
| `PawOrderEvidenceAuthoringWindow.cs` | `PawOrderEvidenceAuthoringWindow` | Janela de preenchimento das pontuações | `Tools/Fabula/Paw Order/Evidence Authoring` |
| `PawOrderEvidenceAuthoringUtility.cs` | `PawOrderEvidenceAuthoringUtility`, `PawOrderEvidenceSuggestion` | Apoio à autoria e sugestões | interno |
| `PawOrderEvidenceBatchSuggestionApplier.cs` | `PawOrderEvidenceBatchSuggestionApplier` | Aplica sugestões em lote | interno |
| `PawOrderEvidenceContextDatabase.cs` | `PawOrderEvidenceContextDatabase` (SO) | Vínculos personagem ↔ local ↔ item | `ReplaceContent`, `GetLocationLinks`, `GetItemLinks` |
| `PawOrderEvidenceContextImporter.cs` | `PawOrderEvidenceContextImporter` | Preenche o banco de contexto a partir da planilha | interno |
| `PawOrderEvidenceContextSuggestionService.cs` | `PawOrderEvidenceContextSuggestionService` | Sugere pontuação a partir do contexto | interno |
| `PawOrderOutcomeEvidenceTool.cs` | `PawOrderOutcomeEvidenceTool` (static) | Gera prévia de pontuação (CSV/JSON) e aplica | `GeneratePreviewOnly`, `GeneratePreviewAndApply`, `RunFromBatchModePreviewAndApply` · `Tools/Paw Order/Investigations/Generate Evidence Preview…` |
| `InteractionOutcomeDataEditor.cs` | `InteractionOutcomeDataEditor` | Inspector customizado de `InteractionOutcomeData` | `OnInspectorGUI` |

## Validação

| Arquivo | Classe | Responsabilidade | API / menu |
|---|---|---|---|
| `PawOrderEvidenceValidatorWindow.cs` | `PawOrderEvidenceValidatorWindow` | Valida o equilíbrio das evidências entre suspeitos | `Tools/Fabula/Paw Order/Validate Evidence Balance` |
| `PawOrderEvidenceImpactValidatorWindow.cs` | `PawOrderEvidenceImpactValidatorWindow` | Janela do validador de impacto | `Tools/Fabula/Paw Order/Evidence Impact Validator` |
| `PawOrderEvidenceImpactValidatorService.cs` | `PawOrderEvidenceImpactValidatorService` | Compara assets com a verdade e o questionário | `Validate` |
| `PawOrderEvidenceImpactReport.cs` | `PawOrderEvidenceImpactReport` (+ `Row`, `Issue`, `Severity`) | Relatório do validador de impacto | `AddRow`, `AddIssue`, `BuildMarkdown`, `BuildCsv`, `GetScoreBand` |

## Menus e wizards de cena

| Arquivo | Classe | Responsabilidade | Menu |
|---|---|---|---|
| `QuestionPromptMenuOrganizerWindow.cs` | `QuestionPromptMenuOrganizerWindow` | Organiza grupo, alvo e ordem das perguntas | `Fabula/Paw Order/Question Prompt Menu Organizer` |
| `Characters/CharacterSceneActorWizard.cs` | `CharacterSceneActorWizard` | Cria um personagem de cena configurado | `Fabula/Paw Order/Investigations/Create Character Scene Actor` |
| `Questions/QuestionBranchMenuRuntimeWizard.cs` | `QuestionBranchMenuRuntimeWizard` | Cria a UI do menu em ramos | `Fabula/Paw Order/Create Question Branch Menu Runtime UI` |
| `Questions/QuestionMenuExampleUiWizard.cs` | `QuestionMenuExampleUiWizard` (static) | Cria a UI do menu de exemplo | `Fabula/Paw Order/Create Question Menu Example UI` |

## Entradas e saídas

| Caminho | Papel |
|---|---|
| `Assets/Project/Documents/*.xlsx` | Planilhas de origem |
| `Assets/Project/Scriptables/Investigations/PawOrder/` | Assets gerados |
| `…/PawOrder/Editor/PawOrderEvidenceContextDatabase.asset` | Banco de contexto |
| `…/PawOrder/Reports/` | Relatórios dos validadores |
| `…/PawOrder/Outcomes/PawOrder_OutcomeEvidencePreview.{csv,json}` | Prévia de pontuação |
