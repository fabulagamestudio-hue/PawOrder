# Mapa — Tools / Animation

- **Caminho:** `Assets/Project/Scripts/Tools/Animation/`
- **Namespace:** `Iung.Animation`
- **Assemblies:** `Iung.Animation.Runtime` (`Runtime/`), `Iung.Animation.Editor` (`Editor/`)
- **Propósito:** animação de UI configurada no Inspector, baseada em listas de `AnimationProperty` e DOTween.
- **Guias:** [AnimationSystemGuide.md](AnimationSystemGuide.md) · [README.md](README.md) (documento original do pacote)

## `Runtime/`

| Arquivo | Classe | Responsabilidade | API principal |
|---|---|---|---|
| `AnimationProperty.cs` | `AnimationProperty` (+ `AnimationType`, `IdleSyncOptions`) | Configuração de uma animação de mostrar/esconder | `ConfigureStartPosition`, `ForceStart`, `ForceEnd`, `Show`, `Hide` |
| `AnimationPropertyController.cs` | `AnimationPropertyController` (extensões) | Opera listas de `AnimationProperty` | `ConfigureStartPosition`, `ForceStartPosition`, `ForceEndPosition`, `RevealThis`, `HideThis`, `IsRevealed`, `IsAnimating` |
| `UIAnimationChannels.cs` | `UIAnimationChannels` (enum) | Canais de animação usados pelos idles | — |
| `UI_RectTransformTweener.cs` | `UI_RectTransformTweener`, `RectTransformState` | Tween entre dois estados de `RectTransform` | `ToggleState`, `ForceState`, `AnimateTowards` |
| `UI_ButtonAnimation.cs` | `UI_ButtonAnimation` | Animações de botão por evento de ponteiro | `Configure_Entry`, `Configure_Click`, `Configure_Exit`, `Configure_*Drag`, `Configure_Scroll` |
| `UI_ScreenSequenceController.cs` | `UI_ScreenSequenceController` (+ `ScreenGroup`, `ScreenAdvanceMode`) | Sequência de telas | `PlayFirstScreen`, `NextScreen`, `PrevScreen`, `GoToScreen`, `Continue` |
| `UI_AnimationListFlowTester.cs` | `UI_AnimationListFlowTester` | Componente de teste de uma lista | `ShowList`, `HideList`, `ForceHidden`, `ForceShown` |

## `Runtime/Idle/`

| Arquivo | Classe | Responsabilidade | API principal |
|---|---|---|---|
| `UI_IdleBase.cs` | `UI_IdleBase` (abstrata), `IdlePauseHandle` | Base dos idles e pausa por canal | `RefreshBasePose`, `AcquireExternalPause`, `ReleaseExternalPause`, `PauseForTarget` |
| `UI_FloatIdle.cs` | `UI_FloatIdle` | Flutuação | Inspector |
| `UI_WiggleIdle.cs` | `UI_WiggleIdle` | Balanço de rotação | Inspector |
| `UI_FloatAndWiggleIdle.cs` | `UI_FloatAndWiggleIdle` | Flutuação + balanço com controle de intensidade | `SetIntensity`, `SetFloatSpeedHz`, `SetWiggleSpeedHz`, `SetSpeedMultiplier`, `RebuildIdle` |
| `UI_PulseScaleIdle.cs` | `UI_PulseScaleIdle` | Pulso de escala | Inspector |
| `UI_ShakeBurstIdle.cs` | `UI_ShakeBurstIdle` | Tremores em rajadas | Inspector |

## `Editor/`

| Arquivo | Classe | Responsabilidade | API / menu |
|---|---|---|---|
| `AnimationProperty_PropertyDrawer.cs` | `AnimationProperty_PropertyDrawer` | Drawer de `AnimationProperty` | `OnGUI`, `GetPropertyHeight` |
| `AnimationProperty_ScenePreview.cs` | — | Pré-visualização na Scene View | — |
| `AnimationPropertyHierarchyIconCache.cs` | `AnimationPropertyHierarchyIconCache` | Ícones na Hierarchy | `MarkDirtyAndRepaint` |
| `RectTransformTweenerEditor.cs` | `RectTransformTweenerEditor` | Inspector de `UI_RectTransformTweener` | `OnInspectorGUI` |
| `Config/AnimationGlobalSettings.cs` | `AnimationGlobalSettings` (SO), `AnimationSettingsLogLevel` | Valores padrão globais do pacote | `ResetToPluginDefaults` |
| `Services/AnimationSettingsService.cs` | `AnimationSettingsService` (static) | Localiza/cria o asset de configurações | `GetOrCreateSettings`, `SaveSettings`, `SelectSettingsAsset` |
| `Services/AnimationSettingsApplyService.cs` | `AnimationSettingsApplyService` (static) | Aplica os padrões à seleção | `ApplyDefaultsToAnimationProperty`, `ApplyDefaultsToSelected*` |
| `Services/AnimationPaths.cs` | `AnimationPaths` (static) | Constantes de caminho | — |
| `Services/AnimationPathUtility.cs` | `AnimationPathUtility` (static) | Normalização de caminhos | `NormalizeAssetPath`, `IsAssetsPath`, `NormalizeSystemPath` |
| `Windows/AnimationSettingsWindow.cs` | `AnimationSettingsWindow` | Janela de configurações | `Tools/Iung/Animation/Settings` |
