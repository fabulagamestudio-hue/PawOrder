# Paw & Order

Jogo de investigação **não linear** com personagens animais em clima noir. O jogador explora locais, coleta pistas e interroga personagens; ao final pode **declarar qualquer personagem como culpado**, desde que apresente as justificativas certas (motivo, meio e oportunidade). Não existe um "culpado único" travado no código — a acusação é sustentada pelas provas que o jogador reuniu.

- **Plataforma primária:** PC (Windows), lançamento na **Steam**.
- **Demo:** apenas o **primeiro caso** (Blue Canary — morte de Lola Veludo), distribuída na **Steam e no itch.io**.
- **Idioma do conteúdo e da documentação:** português (pt-BR). Identificadores de código em inglês.

## Stack

| Item | Valor |
|---|---|
| Engine | Unity **6000.3.11f1** (Unity 6) |
| Render | URP 17.3 (`Assets/Project/Settings/PC_RPAsset.asset`) |
| Input | Input System novo (único handler ativo) — `Assets/InputSystem_Actions.inputactions` |
| UI | uGUI + TextMeshPro |
| Tween | DOTween (`Assets/Plugins/Demigiant`) |
| Câmera | Cinemachine 3 |
| Cena de build | `Assets/Project/Scenes/SampleScene.unity` (única) |

Não há pipeline de build nem testes automatizados: compilar, rodar e buildar é feito pelo Editor da Unity. Não edite `.csproj`/`.slnx` (gerados e ignorados pelo git).

## Onde está cada coisa

Todo conteúdo próprio do jogo vive em `Assets/Project/`. O resto de `Assets/` é de terceiros.

| Caminho | Conteúdo |
|---|---|
| `Assets/Project/Scripts/Investigations/` | Sistema de investigação (`Data`, `Runtime`, `Editor`) — namespace `Fabula.PawOrder` / `Fabula.PawOrder.Editor` |
| `Assets/Project/Scripts/Tools/Animation/` | Animação de UI reutilizável — namespace `Iung.Animation` (tem `.asmdef` próprio) |
| `Assets/Project/Scripts/Tools/Shared/` | Utilitários genéricos — namespace `Iung.Tools.Shared.*` |
| `Assets/Project/Scriptables/Investigations/PawOrder/` | Assets do caso: `Cases`, `Characters`, `Locations`, `Items`, `References`. Perguntas, interações e respostas foram removidas na V2 e serão recriadas |
| `Assets/Project/Documents/` | Fonte narrativa (planilhas `.xlsx` e dossiês `.docx`) |
| `Assets/Project/Art`, `Prefabs`, `Fonts`, `SFX` | Arte, prefabs de UI, fontes e áudio |
| `Tools/` (raiz) | Scripts PowerShell de manutenção dos assets do caso |
| `Assets/Feel`, `Assets/Plugins`, `Assets/TextMesh Pro` | **Terceiros — não modificar** |

### Documentação (leia antes de mexer na pasta)

A raiz da documentação é `Assets/Project/Scripts/`, seguindo a separação **mapa × guia × `KnownIssues.md`** das regras globais.

- **Mapas** — `AI_GUIDE.md` em cada pasta de código; comece por [Scripts/AI_GUIDE.md](Assets/Project/Scripts/AI_GUIDE.md).
- **Pontos de atenção** — [KnownIssues.md](Assets/Project/Scripts/KnownIssues.md).
- **Guias de sistema:**
  - [PawOrderScriptsOverview.md](Assets/Project/Scripts/PawOrderScriptsOverview.md)
  - [InvestigationSystemGuide.md](Assets/Project/Scripts/Investigations/InvestigationSystemGuide.md) → [Data](Assets/Project/Scripts/Investigations/Data/InvestigationDataGuide.md), [Runtime](Assets/Project/Scripts/Investigations/Runtime/InvestigationRuntimeGuide.md), [Editor](Assets/Project/Scripts/Investigations/Editor/InvestigationEditorToolsGuide.md)
  - [ToolsPackageGuide.md](Assets/Project/Scripts/Tools/ToolsPackageGuide.md) → [Animation](Assets/Project/Scripts/Tools/Animation/AnimationSystemGuide.md) (+ `README.md` original), [Shared](Assets/Project/Scripts/Tools/Shared/SharedUtilitiesGuide.md)

Arquivos `.md` novos dentro de `Assets/` também precisam do `.meta` gerado pela Unity.

## Modelo do jogo (essencial)

- `CaseData` descreve um caso: locais, personagens, suspeitos, prompts iniciais.
- `InvestigationPromptData` (abstrata) é tudo que o jogador pode "usar" numa conversa: `ItemPromptData`, `QuestionPromptData`, `ReferencePromptData`.
- `CharacterInteractionData` = personagem + prompt + requisitos → `InteractionOutcomeData` (resposta, provas, desbloqueios).
- `InteractionOutcomeData.SuspectEvidenceValues` carrega, **por suspeito**, pontuação de `motivation` / `means` / `opportunity`. É isso que torna qualquer suspeito acusável.
- `CaseRuntimeState` é a única fonte de verdade do progresso da sessão (locais, itens, prompts, outcomes descobertos, `SelectedCulprit`).

## Regras de projeto

1. **Culpado é dado, nunca código.** Não escreva lógica que assuma um culpado fixo, nem compare `characterId`/nomes de suspeitos no runtime. Toda conclusão deve derivar das provas (`SuspectEvidenceValue`) e do estado em `CaseRuntimeState`.
2. **Não linearidade.** Nenhum sistema pode exigir uma ordem de visita a locais, perguntas ou pistas além do que está declarado em `InteractionRequirements` e nos desbloqueios dos outcomes. Ao adicionar um requisito, verifique que não cria beco sem saída.
3. **Runtime agnóstico de caso.** A demo tem um caso, o jogo completo terá vários. Código em `Runtime`/`Data` não referencia conteúdo do Blue Canary; conteúdo específico fica em `Scriptables/Investigations/<Caso>/`. (As ferramentas `PawOrder*` do Editor são hoje específicas deste caso — tudo bem, mas não deixe isso vazar para o runtime.)
4. **Dependências em uma direção:** `Investigations` → `Tools`. `Tools` nunca conhece `Fabula.PawOrder`. Código de Editor só em pastas `Editor`.
5. **Demo roda sem Steam.** A mesma demo vai para o itch.io; qualquer integração com Steamworks precisa ser isolada e opcional, com o jogo funcionando sem o cliente Steam.
6. **PC primeiro.** Interação é por mouse (hover, clique, arrastar lupa). Os assets `Mobile_*` de URP são do template; não otimize para mobile.
7. **Conteúdo narrativo vem das planilhas.** Mudanças em massa de falas/perguntas devem passar por importador, não por edição manual de dezenas de `.asset`. Na V2 os importadores e validadores antigos foram removidos (ver `KnownIssues.md`, KI-13); o novo fluxo ainda será construído.
8. **Texto em UTF-8.** Os `.asset` contêm pt-BR com acentos e já sofreram mojibake. Ao editar YAML por script, leia e grave explicitamente em UTF-8.
9. **Assets da Unity:** todo arquivo novo precisa do seu `.meta`; nunca altere GUIDs nem mova/renomeie assets fora do Editor sem levar o `.meta` junto.
10. Comentários `AI GUIDANCE` nos arquivos de `Tools/Shared` são regra local prioritária.

## Convenções de código

- Campos `[SerializeField] private` em camelCase, expostos por propriedades somente leitura (`IReadOnlyList<>` para listas).
- `ScriptableObject`s de dados são `sealed`, com `[CreateAssetMenu(menuName = "Fabula/Paw Order/Investigations/...")]`.
- Referencie assets por tipo (`CharacterData`, `LocationData`...), não por string, sempre que a referência existir.
- Transições de UI usam DOTween através de `Iung.Animation` (`AnimationProperty` + `AnimationPropertyController`).

## Cuidados conhecidos

Ficam em [KnownIssues.md](Assets/Project/Scripts/KnownIssues.md). Consulte antes de mexer no sistema de perguntas e respostas (em reconstrução na V2) ou no fluxo de acusação (ainda não implementado no runtime).
