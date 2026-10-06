# Mapa — Scripts

- **Caminho:** `Assets/Project/Scripts/`
- **Propósito:** raiz de todo o código próprio do jogo e da documentação técnica.

## Subpastas

| Pasta | Namespace | Conteúdo | Mapa |
|---|---|---|---|
| `Investigations/` | `Fabula.PawOrder` | Sistema de investigação do jogo | [AI_GUIDE.md](Investigations/AI_GUIDE.md) |
| `Tools/` | `Iung.*` | Pacotes reutilizáveis, independentes do jogo | [AI_GUIDE.md](Tools/AI_GUIDE.md) |

## Arquivos soltos

| Arquivo | Classe | Responsabilidade | API principal |
|---|---|---|---|
| `BloomPulse.cs` | `BloomPulse` (sem namespace) | Pulsa a intensidade do Bloom do `Volume` reagindo a um `AudioSource` | Configuração via Inspector |
| `CameraHandheld.cs` | `CameraHandheld` (sem namespace) | Balanço de câmera na mão (posição e rotação) | Campos públicos de amplitude/frequência |

## Documentação

| Arquivo | Tipo |
|---|---|
| [PawOrderScriptsOverview.md](PawOrderScriptsOverview.md) | Guia geral de arquitetura dos scripts |
| [KnownIssues.md](KnownIssues.md) | Bugs, dívidas e pontos de atenção |
| [`CLAUDE.md`](../../../CLAUDE.md) | Regras do projeto (raiz do repositório) |
