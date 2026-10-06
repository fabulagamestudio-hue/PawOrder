# Mapa — Investigations / Editor

- **Caminho:** `Assets/Project/Scripts/Investigations/Editor/`
- **Namespace:** `Fabula.PawOrder.Editor`
- **Propósito:** ferramentas de autoria do caso — leitura de planilhas, organização de menus e wizards de cena.
- **Guia:** [InvestigationEditorToolsGuide.md](InvestigationEditorToolsGuide.md) · sistema: [InvestigationSystemGuide.md](../InvestigationSystemGuide.md)

## Leitura de planilhas

| Arquivo | Classe | Responsabilidade | API / menu |
|---|---|---|---|
| `PawOrderExcelWorkbookReader.cs` | `PawOrderExcelWorkbookReader` | Leitor de `.xlsx` | `Load`, `TryGetSheet` |

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
| `Assets/Project/Scriptables/Investigations/PawOrder/` | Assets do caso |
