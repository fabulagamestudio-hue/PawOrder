# Investigation Editor Tools Guide

## Purpose
A pasta `Editor` contém ferramentas de autoria e validação para o conteúdo investigativo do Paw&Order. Essas ferramentas ajudam a importar casos, organizar perguntas, validar evidências e criar estruturas de UI/cena.

## Main tool groups
- Leitura de planilhas: `PawOrderExcelWorkbookReader`.
- Organização de perguntas: `QuestionPromptMenuOrganizerWindow`.
- Wizards: criação de atores de cena e exemplos de UI de perguntas.

## Estado na V2
Os importadores de caso/perguntas e todas as ferramentas de pontuação e validação de evidência da V1 foram removidos (continuam na branch `main`). O novo fluxo de autoria de perguntas e respostas ainda não existe — ver [KnownIssues.md](../../KnownIssues.md), KI-13.

## Namespace
Todos os scripts desta pasta devem usar `Fabula.PawOrder.Editor`.

## Rules and conventions
- Ferramentas de Editor não devem ser referenciadas por scripts de runtime.
- Serviços que fazem processamento devem ficar separados de janelas sempre que possível.
- Janelas devem coordenar input do usuário e exibição de resultados, não concentrar toda a regra de negócio.
- Operações que criam ou alteram assets devem ser cuidadosas com paths, nomes e duplicações.

## Prefer
- Usar serviços para lógica reutilizável de validação, sugestão e atualização.
- Manter relatórios em tipos dedicados quando a ferramenta produz diagnóstico.
- Criar métodos pequenos ao evoluir importadores grandes.
- Preservar compatibilidade com assets já criados.

## Avoid
- Aumentar ainda mais classes grandes sem necessidade, especialmente importadores e janelas complexas.
- Misturar leitura de arquivo, transformação de dados, criação de assets e UI na mesma função.
- Criar ferramentas que dependam de objetos de cena quando a operação deveria agir sobre assets.
- Alterar estrutura de pastas de assets gerados sem documentar o impacto.

## Notes for AI assistants
Antes de modificar uma ferramenta grande, localize o fluxo exato usando nomes de métodos, menu items e chamadas de serviço. Prefira extrair helpers pequenos a reescrever a ferramenta inteira.
