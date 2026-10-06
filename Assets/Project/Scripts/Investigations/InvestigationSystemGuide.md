# Investigation System Guide

## Purpose
O módulo `Investigations` implementa o sistema narrativo investigativo de Paw&Order. Ele combina dados em `ScriptableObject`, estado de runtime, interações com personagens, pistas, perguntas, salas e ferramentas de autoria no Editor.

## Namespace
Todos os scripts de runtime e dados usam o namespace `Fabula.PawOrder`. Scripts de Editor usam `Fabula.PawOrder.Editor`.

## Main areas
- `Data`: define os assets de caso, personagens, locais, pistas, perguntas, outcomes e enums de organização.
- `Runtime`: controla o estado ativo da investigação e os fluxos jogáveis.
- `Editor`: oferece ferramentas para importar, validar, organizar e gerar conteúdo investigativo.

## Core flow
1. Um `CaseData` define o caso inicial, locais desbloqueados, prompts iniciais, suspeitos e índices de conteúdo.
2. `CaseRuntimeState` instancia o estado jogável a partir do `CaseData`.
3. Interações com personagens, pistas ou salas consultam e modificam o `CaseRuntimeState`.
4. `InteractionOutcomeData` pode desbloquear novos prompts e locais.
5. Menus de pergunta são construídos a partir dos prompts desbloqueados e das condições do NPC atual.

## Rules and conventions
- Preserve o namespace `Fabula.PawOrder` para runtime e dados.
- Preserve o namespace `Fabula.PawOrder.Editor` para ferramentas de Editor.
- Use `ScriptableObject` para conteúdo narrativo estático.
- Use `CaseRuntimeState` para progresso de sessão e desbloqueios temporários.
- Não coloque lógica de Editor em pastas de runtime.
- Não acesse dados narrativos por string quando já existir referência tipada de asset.

## Prefer
- Reutilizar `CaseRuntimeState` para checar prompts, itens, locais e outcomes já conhecidos.
- Separar dados de autoria (`Data`) de comportamento jogável (`Runtime`).
- Criar métodos pequenos quando novas regras de desbloqueio forem adicionadas.
- Adicionar validações em ferramentas de Editor quando uma regra depende da consistência dos assets.

## Avoid
- Duplicar listas de estado fora de `CaseRuntimeState` sem necessidade clara.
- Criar dependências circulares entre dados, runtime e Editor.
- Alterar enums de menu sem revisar builders, views e ferramentas de organização.
- Modificar ferramentas de importação grandes sem antes identificar o trecho específico responsável pelo fluxo afetado.

## Notes for AI assistants
Ao analisar bugs, identifique primeiro se o problema é de dado, estado de runtime, UI ou ferramenta de Editor. Para mudanças de conteúdo investigativo, verifique `Data` e `Editor`. Para comportamento durante o jogo, verifique `Runtime` e os consumidores de `CaseRuntimeState`.
