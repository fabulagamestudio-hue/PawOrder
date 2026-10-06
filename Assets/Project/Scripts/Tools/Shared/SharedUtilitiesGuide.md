# Shared Utilities Guide

## Purpose
A pasta `Shared` contém utilitários genéricos usados para reduzir código repetido em sistemas de gameplay, UI e Editor.

## Main areas
- `Collections`: extensões e helpers para listas e coleções.
- `Lifecycle`: utilitários de ciclo de vida, como persistência entre cenas.
- `Math`: helpers matemáticos e aleatoriedade.
- `Pooling`: contratos e reset de objetos reaproveitados.
- `Transforms`: operações comuns de transform.
- `UI`: utilitários de UI e TextMeshPro.
- `Editor`: ferramentas de criação, renomeação, TMP e transform.

## Pooling rule
Use `PoolStateRestorer` quando prefabs reutilizados precisarem restaurar transform, estado ativo e componentes habilitados para o snapshot inicial. Use `IPoolResettable` para componentes que precisam de reset específico.

## UI rule
Use `UIUtils.SetTextFromLines` para preencher `TextMeshProUGUI` a partir de linhas. Use `UIUtils.IsPointerOverUI` para evitar duplicação de checagens comuns de pointer sobre UI.

## Rules and conventions
- Utilitários compartilhados devem ser genéricos e não depender de features específicas.
- Comentários `AI GUIDANCE` são orientação explícita e devem ser respeitados.
- Prefira métodos de extensão quando o utilitário melhora a legibilidade do consumidor.
- Ferramentas de Editor devem permanecer em subpastas `Editor`.

## Prefer
- Reutilizar helpers existentes antes de criar novos.
- Manter cada utilitário pequeno e focado.
- Usar `PoolStateRestorer` para objetos complexos em pool.
- Usar TextMeshPro para textos de UI.

## Avoid
- Criar utilitários com dependência de `Fabula.PawOrder`.
- Repetir boilerplate de UI ou pooling em scripts de gameplay.
- Usar reset manual espalhado quando o prefab pode usar `PoolStateRestorer`.
- Colocar código de Editor fora de pastas `Editor`.

## Notes for AI assistants
Antes de criar um novo helper, pesquise nesta pasta por comportamento semelhante. Se um comentário `AI GUIDANCE` existir no arquivo afetado, trate-o como regra local prioritária.
