# Known Issues — Paw & Order

Bugs, dívidas técnicas e pontos de atenção. Cada item cita o guia afetado. Ao resolver um item, remova-o daqui (o histórico fica no git).

Levantamento inicial: 2026-10-06.

## Sistema de perguntas e respostas (V2)

### KI-13 — Conteúdo e ferramentas de perguntas/respostas removidos
- **Onde:** `Scriptables/Investigations/PawOrder/` e `Investigations/Editor/`
- **Problema:** a branch `v2` apagou os assets de `Questions`, `Interactions` e `Outcomes`, o importador do caso e todas as ferramentas de pontuação/validação de evidência (a V1 segue na `main`). O modelo (`QuestionPromptData`, `CharacterInteractionData`, `InteractionOutcomeData`, `SuspectEvidenceValue`) e a UI de `Runtime/Questions` continuam, mas sem dados: `CaseData.startingPrompts` está vazio e os `CharacterSceneActor` da `SampleScene` não têm interações. Também não há mais ferramenta que gere personagens, locais e itens a partir das planilhas.
- **Pendência:** definir o novo modelo de respostas e evidência (escala dos eixos, como uma fala influencia outra) e o novo fluxo de importação.
- **Guia afetado:** [InvestigationEditorToolsGuide.md](Investigations/Editor/InvestigationEditorToolsGuide.md), [InvestigationDataGuide.md](Investigations/Data/InvestigationDataGuide.md)

### KI-14 — `CaseData.suspects` vazio
- **Onde:** `Scriptables/Investigations/PawOrder/Cases/PawOrder_MainCase.asset`
- **Problema:** a lista de suspeitos do caso nunca foi preenchida; os seis personagens só estão marcados por `CharacterData.IsSuspect`.
- **Guia afetado:** [InvestigationDataGuide.md](Investigations/Data/InvestigationDataGuide.md)

## Conteúdo do caso

### KI-05 — Textos com acentuação corrompida (mojibake)
- **Onde:** `.asset` em `Assets/Project/Scriptables/Investigations/PawOrder/`
- **Problema:** os assets já tiveram texto pt-BR corrompido por gravação em codificação errada; `Tools/fix_paworder_encoding.ps1` existe para reparar.
- **Cuidado:** qualquer script que edite esses YAML deve ler e gravar em UTF-8 explicitamente.
- **Guia afetado:** [InvestigationDataGuide.md](Investigations/Data/InvestigationDataGuide.md)

## Runtime

### KI-07 — Fluxo de acusação ainda não implementado
- **Onde:** `Investigations/Runtime/`
- **Problema:** a mecânica central (declarar um culpado com justificativas) só tem esqueleto: `CaseRuntimeState.SelectedCulprit` e os tipos `PlannerNodeRuntime`, `PlannerConnectionRuntime`, `PlannerNodeType`. Nada no runtime lê `SuspectEvidenceValue` para chegar a um veredito.
- **Guia afetado:** [InvestigationRuntimeGuide.md](Investigations/Runtime/InvestigationRuntimeGuide.md)

### KI-08 — Scripts soltos sem namespace
- **Onde:** `Scripts/BloomPulse.cs`, `Scripts/CameraHandheld.cs`
- **Problema:** estão no namespace global e fora de qualquer módulo.
- **Guia afetado:** [PawOrderScriptsOverview.md](PawOrderScriptsOverview.md)

### KI-09 — Namespace divergente em `DontDestroyOnLoadObject`
- **Onde:** `Tools/Shared/Lifecycle/DontDestroyOnLoadObject.cs`
- **Problema:** usa `Iung.Tools.Core.Lifecycle`; o restante de `Shared` usa `Iung.Tools.Shared.*`. Renomear exige atualizar quem importa o namespace.
- **Guia afetado:** [SharedUtilitiesGuide.md](Tools/Shared/SharedUtilitiesGuide.md)

## Projeto e lançamento

### KI-10 — Arquivos fora do lugar em `Scripts/`
- **Onde:** `Scripts/Investigations.zip`, `Scripts/Ashes on Mulberry.mp3`
- **Problema:** um `.zip` (cópia de scripts) e uma música estão versionados na pasta de código. O áudio pertence a `Assets/Project/SFX` ou a uma pasta de música; o `.zip` provavelmente pode sair do repositório. Mover pelo Editor para preservar o `.meta`.
- **Guia afetado:** [PawOrderScriptsOverview.md](PawOrderScriptsOverview.md)

### KI-11 — Player Settings ainda com valores de template
- **Onde:** `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/EditorBuildSettings.asset`
- **Problema:** `companyName` é `DefaultCompany` (define a pasta de dados persistentes, portanto de saves), a resolução padrão é 1024×768 e a única cena de build é `SampleScene`. Ajustar antes de publicar a demo na Steam/itch.io — trocar o `companyName` depois do lançamento muda o local dos saves.
- **Guia afetado:** nenhum (regras em [`CLAUDE.md`](../../../CLAUDE.md))

### KI-12 — Pacote Feel importado sem uso
- **Onde:** `Assets/Feel/`
- **Problema:** nenhum script do projeto referencia `MoreMountains`. O pacote (com demos) responde pela maior parte dos arquivos versionados.
- **Sugestão:** adotar ou remover; se ficar, remover ao menos `FeelDemos`.
- **Guia afetado:** nenhum
