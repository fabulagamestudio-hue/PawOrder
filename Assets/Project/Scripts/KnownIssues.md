# Known Issues — Paw & Order

Bugs, dívidas técnicas e pontos de atenção. Cada item cita o guia afetado. Ao resolver um item, remova-o daqui (o histórico fica no git).

Levantamento inicial: 2026-10-06.

## Ferramentas de autoria

### KI-01 — Caminho absoluto antigo no importador do caso
- **Onde:** `Investigations/Editor/PawOrderCaseBootstrapImporter.cs:17` (`XlsxPath`)
- **Problema:** aponta para `D:/Projetos Unity/Fabula/Documents/Paw&Order/FalemeSobreX_OrdemFinalPerguntas.xlsx`, fora do repositório e de uma máquina antiga. O menu `Import Paw & Order Case Assets` não encontra a planilha no ambiente atual.
- **Sugestão:** usar a cópia versionada em `Assets/Project/Documents/`, como já é feito para `blue-canary-personagens.xlsx` na linha seguinte.
- **Guia afetado:** [InvestigationEditorToolsGuide.md](Investigations/Editor/InvestigationEditorToolsGuide.md)

### KI-02 — `paworder_apply_scores.ps1` desatualizado
- **Onde:** `Tools/paworder_apply_scores.ps1` (raiz do repositório)
- **Problema:** o `ProjectRoot` padrão é `D:/Projetos Unity/Fabula/Games/Paw-Order` e o script lê `paworder_import_source.json`, que não existe no repositório.
- **Sugestão:** decidir se o script ainda é necessário, já que `PawOrderOutcomeEvidenceTool` cobre a mesma função dentro do Editor; se for, derivar a raiz de `$PSScriptRoot`.
- **Guia afetado:** [InvestigationEditorToolsGuide.md](Investigations/Editor/InvestigationEditorToolsGuide.md)

### KI-03 — Menus das ferramentas espalhados em três raízes
- **Onde:** `Investigations/Editor/` (`[MenuItem]`)
- **Problema:** as ferramentas aparecem em `Fabula/Paw Order/...`, `Tools/Fabula/Paw Order/...` e `Tools/Paw Order/Investigations/...`.
- **Sugestão:** unificar em uma raiz só.
- **Guia afetado:** [InvestigationEditorToolsGuide.md](Investigations/Editor/InvestigationEditorToolsGuide.md)

### KI-04 — Ferramentas de Editor presas ao caso Blue Canary
- **Onde:** classes `PawOrder*` em `Investigations/Editor/`
- **Problema:** pastas, nomes de planilha e de suspeitos estão fixos para o primeiro caso. Suficiente para a demo; vira retrabalho no segundo caso.
- **Guia afetado:** [InvestigationEditorToolsGuide.md](Investigations/Editor/InvestigationEditorToolsGuide.md)

## Conteúdo do caso

### KI-05 — Textos com acentuação corrompida (mojibake)
- **Onde:** `.asset` em `Assets/Project/Scriptables/Investigations/PawOrder/`
- **Problema:** os assets já tiveram texto pt-BR corrompido por gravação em codificação errada; `Tools/fix_paworder_encoding.ps1` existe para reparar.
- **Cuidado:** qualquer script que edite esses YAML deve ler e gravar em UTF-8 explicitamente.
- **Guia afetado:** [InvestigationDataGuide.md](Investigations/Data/InvestigationDataGuide.md)

### KI-06 — Outcome de teste entre os assets do caso
- **Onde:** `Scriptables/Investigations/PawOrder/Outcomes/START_56_Teste/`
- **Problema:** aparenta ser conteúdo de teste. Verificar e remover (junto com pergunta e interações associadas) antes da demo.
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
