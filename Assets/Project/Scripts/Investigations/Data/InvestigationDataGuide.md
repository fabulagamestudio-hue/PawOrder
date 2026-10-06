# Investigation Data Guide

## Purpose
A pasta `Data` contém os modelos de conteúdo investigativo usados pelo Paw&Order. Esses scripts descrevem casos, personagens, locais, perguntas, pistas, outcomes e estados de disponibilidade.

## Main files
- `CaseData.cs`: asset principal de um caso investigativo.
- `CharacterData.cs`: dados estáticos de personagens e suspeitos.
- `LocationData.cs`: dados estáticos de locais investigáveis.
- `InvestigationPromptData.cs`: base de prompts investigativos.
- `QuestionPromptData.cs`: perguntas disponíveis nos menus de investigação.
- `ItemPromptData.cs`: pistas ou itens coletáveis que também funcionam como prompts.
- `InteractionOutcomeData.cs`: consequências de interações, incluindo desbloqueios.
- `CharacterInteractionData.cs`: define interações possíveis com personagens e seus requisitos.

## Data ownership
`CaseData` funciona como índice central do caso. Ele referencia o local inicial, locais desbloqueados no início, prompts iniciais, suspeitos e listas completas de conteúdo.

## Rules and conventions
- Os dados devem permanecer em `ScriptableObject` quando representam conteúdo autoral estático.
- Campos serializados devem ser privados, expostos por propriedades públicas somente leitura quando necessário.
- Prefira referências diretas entre assets, evitando IDs textuais para relações internas.
- Enums devem ser tratados como contrato de UI e autoria; alterar valores pode afetar assets já configurados.

## Prefer
- Adicionar novos dados como assets tipados.
- Expor coleções como `IReadOnlyList` quando o runtime só precisa ler.
- Centralizar desbloqueios narrativos em `InteractionOutcomeData` quando a interação muda o estado do caso.
- Manter nomes e menus de `CreateAssetMenu` coerentes com `Fabula/Paw Order/Investigations`.

## Avoid
- Guardar progresso de partida dentro dos assets de dados.
- Usar `ScriptableObject` como estado mutável de sessão.
- Alterar nomes de campos serializados sem plano de migração para assets existentes.
- Criar dependências diretas de UI dentro dos dados.

## Notes for AI assistants
Quando uma mudança exigir novo dado, verifique primeiro se o dado é conteúdo estático ou estado de runtime. Conteúdo estático deve ficar em `Data`; progresso desbloqueado deve ficar em `CaseRuntimeState`.
