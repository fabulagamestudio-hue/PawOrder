# Mapa — Investigations

- **Caminho:** `Assets/Project/Scripts/Investigations/`
- **Namespace:** `Fabula.PawOrder` (dados e runtime), `Fabula.PawOrder.Editor` (ferramentas)
- **Propósito:** sistema de investigação do jogo — dados do caso, estado da sessão, interação com personagens, pistas, perguntas e salas, mais as ferramentas de autoria.
- **Guia:** [InvestigationSystemGuide.md](InvestigationSystemGuide.md)

## Subpastas

| Pasta | Conteúdo | Mapa | Guia |
|---|---|---|---|
| `Data/` | `ScriptableObject`s e enums que descrevem um caso | [AI_GUIDE.md](Data/AI_GUIDE.md) | [InvestigationDataGuide.md](Data/InvestigationDataGuide.md) |
| `Runtime/` | Estado da sessão e comportamento jogável | [AI_GUIDE.md](Runtime/AI_GUIDE.md) | [InvestigationRuntimeGuide.md](Runtime/InvestigationRuntimeGuide.md) |
| `Editor/` | Leitor de planilhas, organizador de menus e wizards | [AI_GUIDE.md](Editor/AI_GUIDE.md) | [InvestigationEditorToolsGuide.md](Editor/InvestigationEditorToolsGuide.md) |

## Relacionados fora desta pasta

| Caminho | Conteúdo |
|---|---|
| `Assets/Project/Scriptables/Investigations/PawOrder/` | Assets do caso Blue Canary |
| `Assets/Project/Documents/` | Planilhas e dossiês de origem do conteúdo |
| `Tools/` (raiz do repositório) | Scripts PowerShell de manutenção dos assets |
| [KnownIssues.md](../KnownIssues.md) | Pontos de atenção |
