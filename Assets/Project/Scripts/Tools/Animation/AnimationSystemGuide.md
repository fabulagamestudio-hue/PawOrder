# Animation System Guide

## Purpose
Este guia complementa o documento existente da pasta e resume as regras práticas para evoluir o sistema de animação de UI.

## Main contract
O sistema é baseado em listas de `AnimationProperty`. A lista representa um grupo animado bidirecional, usado tanto para mostrar quanto para esconder UI.

## Required flow
1. Criar e configurar uma `List<AnimationProperty>` no Inspector.
2. Chamar `ConfigureStartPosition()` uma única vez na inicialização.
3. Opcionalmente chamar `ForceStartPosition()` para posicionar o grupo no estado oculto.
4. Usar `RevealThis()` para mostrar.
5. Usar `HideThis()` para esconder.

## Main files
- `Runtime/AnimationProperty.cs`: configuração serializável de uma animação bidirecional.
- `Runtime/AnimationPropertyController.cs`: extensões recomendadas para operar listas.
- `Runtime/UI_RectTransformTweener.cs`: tween direto de `RectTransform`.
- `Runtime/Idle`: animações idle coordenadas por canal.
- `Editor`: drawers, preview e configurações globais.

## Rules and conventions
- Use DoTween para qualquer transição do sistema.
- Prefira operar listas via `AnimationPropertyController`.
- `Show` e `Hide` devem usar a mesma configuração.
- `ConfigureStartPosition()` não deve ser chamado em toda transição.
- Idles devem respeitar canais para evitar conflito com animações principais.

## Prefer
- Expandir o controller e o Inspector antes de criar fluxos paralelos.
- Manter o sistema focado em UI.
- Preservar compatibilidade com listas já configuradas no Inspector.
- Documentar qualquer mudança que altere o contrato de inicialização.

## Avoid
- Criar uma lista separada para show e outra para hide sem necessidade arquitetural clara.
- Reconfigurar posição inicial repetidamente durante o uso normal.
- Usar este pacote como substituto de animação gameplay geral.
- Ignorar idle channels ao adicionar novos tipos de animação.

## Notes for AI assistants
O arquivo `README.md` existente nesta pasta é autoridade histórica do pacote. Este guia existe para manter um nome mais intuitivo e facilitar leitura futura sem remover o documento original.
