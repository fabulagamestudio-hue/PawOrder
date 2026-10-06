# Mapa — Tools / Shared

- **Caminho:** `Assets/Project/Scripts/Tools/Shared/`
- **Namespace:** `Iung.Tools.Shared.<Área>` (Editor: `Iung.Tools.Shared.Editor.<Área>`)
- **Propósito:** utilitários genéricos de runtime e pequenas ferramentas de Editor.
- **Guia:** [SharedUtilitiesGuide.md](SharedUtilitiesGuide.md)

## Runtime

| Arquivo | Classe | Responsabilidade | API principal |
|---|---|---|---|
| `Collections/ListUtils.cs` | `ListUtils` (extensões) | Helpers de lista | `GetRandom`, `RemoveRandom`, `GetRandomExcluding`, `RemoveNulls`, `GetLast`, `DestroyGameObjects`, `LogAll` |
| `Math/RandomUtils.cs` | `RandomUtils` (extensões) | Sorteios a partir de intervalo | `RandomInRange`, `RandomIntInRange`, `RandomIndex` |
| `Transforms/TransformUtils.cs` | `TransformUtils` | Helpers de hierarquia | `GetOrCreateFolderParent`, `DestroyAllChildren`, `GetChildrenOfType`, `GetNearestTo` |
| `UI/UIUtils.cs` | `UIUtils` | Helpers de UI/TMP | `SetTextFromLines`, `IsPointerOverUI` |
| `Pooling/PoolStateRestorer.cs` | `PoolStateRestorer` | Restaura um objeto de pool ao estado inicial | `CaptureSnapshotIfNeeded`, `RestoreToInitialState` |
| `Pooling/IPoolResettable.cs` | `IPoolResettable` | Contrato de reset específico por componente | — |
| `Lifecycle/DontDestroyOnLoadObject.cs` | `DontDestroyOnLoadObject` (`Iung.Tools.Core.Lifecycle`) | Mantém o objeto entre cenas | — |

## `Editor/`

| Arquivo | Classe | Responsabilidade | Menu |
|---|---|---|---|
| `Creation/ProjectScriptsCreateAtTool.cs` | `ProjectScriptsCreateAtTool` | Cria scripts em pasta escolhida / adiciona à seleção | `Assets/Iung/Create at`, `Assets/Iung/Add to selected` |
| `Creation/InspectorComponentRenameTool.cs` | `InspectorComponentRenameTool` | Prefixa o nome do GameObject a partir do componente | `CONTEXT/Component/Iung/Prefix GameObject Name` |
| `Creation/PropertyReferenceRenameTool.cs` | `PropertyReferenceRenameTool` | Renomeia o objeto referenciado pelo nome da variável | Menu de propriedade `Iung/Rename Referenced Object From Variable Name` |
| `Creation/PropertyValueTransferTool.cs` | `PropertyValueTransferTool` | Copia valores de propriedade/componente entre dois objetos | `CONTEXT/Component/Copy Value (A -> B)` |
| `TMP/TMP_AuditScene.cs` | `TMP_AuditScene` | Audita textos TMP da cena ativa | `Tools/TMP/Audit Active Scene` |
| `Transforms/TransformCenterParentContextMenu.cs` | `TransformCenterParentContextMenu` | Centraliza o pai na média dos filhos | `CONTEXT/Transform/Center Parent To Children Average` |
| `Transforms/TransformScalePropagationContextMenu.cs` | `TransformScalePropagationContextMenu` | Propaga a escala do pai aos filhos | `CONTEXT/Transform/Propagate Scale To Immediate Children (Reset Parent)` |
| `UI/RectTransformAnchorEditorUtility.cs` | `RectTransformAnchorEditorUtility` | Âncoras para os cantos | `Iung/RectTransform/Anchors To Corners (Zero Position)` |
