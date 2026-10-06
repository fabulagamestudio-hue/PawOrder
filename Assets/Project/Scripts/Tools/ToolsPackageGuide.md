# Tools Package Guide

## Purpose
A pasta `Tools` reúne sistemas reutilizáveis que não devem depender diretamente do Paw&Order. Eles podem ser usados por outros projetos ou módulos.

## Main packages
- `Animation`: sistema de animação de UI baseado em listas de `AnimationProperty` e DoTween.
- `Shared`: utilitários genéricos de coleções, lifecycle, matemática, pooling, transforms, UI, TMP e ferramentas de Editor.

## Namespace rule
Ferramentas genéricas usam namespaces próprios e não devem depender de `Fabula.PawOrder`.

## Dependency rule
`Tools` pode ser usado por `Investigations`, mas `Tools` não deve conhecer `Investigations`.

## Prefer
- Manter APIs genéricas e reutilizáveis.
- Documentar contratos importantes em guias locais ou comentários `AI GUIDANCE`.
- Criar utilitários pequenos, com responsabilidade única.
- Preservar compatibilidade de namespace e nomes públicos.

## Avoid
- Colocar lógica narrativa, casos, personagens ou pistas dentro de `Tools`.
- Criar dependência circular entre ferramentas genéricas e features do jogo.
- Duplicar utilitários já existentes em subpastas de `Shared`.
- Transformar ferramentas genéricas em sistemas específicos de cena.

## Notes for AI assistants
Quando uma solução parecer reutilizável, considere `Tools/Shared`. Quando ela depender de Paw&Order, mantenha em `Investigations`. Leia o guia local do pacote antes de modificar qualquer ferramenta.
