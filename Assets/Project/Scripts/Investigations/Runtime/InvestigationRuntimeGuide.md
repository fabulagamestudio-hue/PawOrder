# Investigation Runtime Guide

## Purpose
A pasta `Runtime` contém os comportamentos jogáveis do sistema de investigação. Ela controla estado do caso, personagens em cena, coleta de pistas, menus de perguntas e navegação entre salas.

## Main systems
- `CaseRuntimeState`: estado central de sessão da investigação.
- `Characters`: atores de cena, hover, clique e solicitação de interação.
- `Clues`: coleta e manipulação visual de pistas.
- `Questions`: construção e exibição de menus de perguntas.
- `Rooms`: navegação entre regiões/salas e transições de câmera.

## Runtime state rule
`CaseRuntimeState` é o ponto principal para desbloqueios e progresso temporário. Ele deve responder perguntas como: prompt conhecido, item coletado, outcome descoberto e local desbloqueado.

## UI rule
Interfaces de texto devem usar TextMeshPro. As views existentes usam `TextMeshProUGUI` e devem manter esse padrão.

## Animation rule
Transições e feedbacks animados devem usar DoTween. A navegação de salas já usa sequência/tween para câmera e fade.

## Dependency rule
Prefira referências serializadas para dependências de cena. Evite buscar componentes em runtime quando a referência puder ser configurada diretamente no Inspector ou por ferramenta de Editor.

## Prefer
- Separar input, estado e apresentação em classes diferentes.
- Usar eventos para comunicar seleção, interação e mudança de sala.
- Reaproveitar builders e resolvers existentes para menus de pergunta.
- Validar `null` antes de operar assets ou referências de cena.

## Avoid
- Escrever progresso direto em assets de `Data` durante o jogo.
- Criar lógica de pergunta diretamente nas views quando ela pertence ao builder/resolver.
- Fazer a UI decidir desbloqueios narrativos.
- Depender de `Camera.main` em novos fluxos; prefira referência serializada ou injeção explícita.

## Notes for AI assistants
Ao alterar runtime, preserve nomes de eventos e APIs públicas já usadas por views e ferramentas. Se uma nova funcionalidade precisa saber algo do caso, primeiro procure em `CaseRuntimeState` antes de criar outro estado paralelo.
