# Mapa — Investigations / Runtime

- **Caminho:** `Assets/Project/Scripts/Investigations/Runtime/`
- **Namespace:** `Fabula.PawOrder`
- **Propósito:** estado da sessão de investigação e os componentes de cena/UI que o consomem.
- **Guia:** [InvestigationRuntimeGuide.md](InvestigationRuntimeGuide.md) · sistema: [InvestigationSystemGuide.md](../InvestigationSystemGuide.md)

## Raiz

| Arquivo | Classe | Responsabilidade | API principal |
|---|---|---|---|
| `CaseRuntimeState.cs` | `CaseRuntimeState` | Progresso da sessão (fonte única de verdade) | `InitializeFrom`, `Has*`, `AddUnlockedPrompt`, `AddCollectedItem`, `AddUnlockedLocation`, `AddDiscoveredOutcome`, `MeetsRequirements`, `SelectedCulprit` |
| `PlannerNodeRuntime.cs` | `PlannerNodeRuntime` | Nó do quadro de planejamento (esqueleto) | Campos de dados |
| `PlannerConnectionRuntime.cs` | `PlannerConnectionRuntime` | Ligação entre nós do quadro (esqueleto) | Campos de dados |
| `PlannerNodeType.cs` | `PlannerNodeType` (enum) | Tipo de nó do quadro | — |

## `Characters/`

| Arquivo | Classe | Responsabilidade | API principal |
|---|---|---|---|
| `CharacterSceneActor.cs` | `CharacterSceneActor` | Personagem presente na cena | `SetHoverState`, `RequestInteraction`, `HasValidSceneReferences` |
| `CharacterPointerDetector.cs` | `CharacterPointerDetector` | Detecta o ponteiro sobre o personagem | `ConfigureForEditor` |
| `CharacterHoverController.cs` | `CharacterHoverController` | Feedback visual de hover | `ConfigureForEditor` |
| `CharacterInteractionController.cs` | `CharacterInteractionController` | Dispara o pedido de interação | `TryRequestInteraction` |

## `Clues/`

| Arquivo | Classe | Responsabilidade | API principal |
|---|---|---|---|
| `WorldClueCollectible.cs` | `WorldClueCollectible` | Pista coletável posicionada no cenário | `Collect`, `GetDistanceFrom` |
| `ClueCollectionController.cs` | `ClueCollectionController` | Coleta de pistas e registro no estado | `SetCaseRuntimeState` |
| `ClueMagnifierDragController.cs` | `ClueMagnifierDragController` | Arraste da lupa sobre o cenário | `OnBeginDrag`, `OnDrag`, `OnEndDrag` |

## `Questions/`

| Arquivo | Classe | Responsabilidade | API principal |
|---|---|---|---|
| `QuestionMenuBuilder.cs` | `QuestionMenuBuilder` (static) | Monta a árvore grupo → alvo → pergunta | `Build`, `GetGroupLabel` |
| `QuestionPromptAvailabilityResolver.cs` | `QuestionPromptAvailabilityResolver` (static) | Decide se a pergunta está oculta, travada ou disponível | `Resolve` |
| `QuestionPromptTextFormatter.cs` | `QuestionPromptTextFormatter` (static) | Rótulos de menu e pronome de autorreferência | `GetMenuLabel`, `GetTargetLabel`, `ShouldUseSelfPronoun` |
| `QuestionPromptGroupMenu.cs` | `QuestionPromptGroupMenu` | Modelo de um grupo do menu | Construtor + propriedades |
| `QuestionPromptTargetMenu.cs` | `QuestionPromptTargetMenu` | Modelo de um alvo do menu | Construtor + propriedades |
| `QuestionPromptMenuEntry.cs` | `QuestionPromptMenuEntry` | Modelo de uma entrada de pergunta | Construtor + propriedades |

## `Questions/UI/`

| Arquivo | Classe | Responsabilidade | API principal |
|---|---|---|---|
| `QuestionMenuRuntimeConnector.cs` | `QuestionMenuRuntimeConnector` | Liga personagens, estado e views de pergunta | `InitializeRuntimeState`, `OpenForCharacter` |
| `CharacterDialogueInvestigationView.cs` | `CharacterDialogueInvestigationView` | Tela de diálogo com o personagem | `ShowForCharacter`, `RefreshInventory`, `ShowQuestionResponse`, `Show`, `Hide` |
| `QuestionBranchMenuRuntimeView.cs` | `QuestionBranchMenuRuntimeView` | Menu de perguntas em ramos (versão de jogo) | `ShowForCharacter`, `BindMenus`, `ShowForCharacterWithPrompts`, `Refresh`, `Show`, `Hide` |
| `QuestionBranchMenuOptionView.cs` | `QuestionBranchMenuOptionView` | Uma opção do menu em ramos | `Bind`, `SetImmediateState`, `AnimateState` |
| `InvestigationInventoryRuntimeView.cs` | `InvestigationInventoryRuntimeView` | Lista de itens coletados | `ShowItems`, `Hide` |
| `InvestigationInventoryItemView.cs` | `InvestigationInventoryItemView` | Célula de item do inventário | `Bind`, `Show`, `Hide` |
| `QuestionMenuExampleView.cs` | `QuestionMenuExampleView` | Menu de perguntas de exemplo (lista expansível) | `ShowForCharacter`, `Refresh`, `Show`, `Hide` |
| `QuestionMenuGroupExampleView.cs` | `QuestionMenuGroupExampleView` | Grupo do menu de exemplo | `Bind`, `ToggleExpanded`, `SetExpanded` |
| `QuestionMenuTargetExampleView.cs` | `QuestionMenuTargetExampleView` | Alvo do menu de exemplo | `Bind`, `ToggleExpanded`, `SetExpanded` |
| `QuestionMenuEntryExampleView.cs` | `QuestionMenuEntryExampleView` | Entrada do menu de exemplo | `Bind` |
| `QuestionResponseExampleView.cs` | `QuestionResponseExampleView` | Painel de resposta do personagem | `ShowResponse`, `Show`, `Hide` |

## `Rooms/`

| Arquivo | Classe | Responsabilidade | API principal |
|---|---|---|---|
| `RoomRegion.cs` | `RoomRegion` | Uma sala na cena e seu alvo de câmera | `HasValidCameraTarget` |
| `RoomNavigationController.cs` | `RoomNavigationController` | Viagem entre salas | `CanTravelTo`, `TravelTo`, `SetCurrentRoomWithoutTransition` |
| `UI/RoomTravelMenuRevealController.cs` | `RoomTravelMenuRevealController` | Exibição do menu de viagem | `ShowMenu`, `HideMenu`, `ForceHideMenu`, `SetRegionBlocked` |
