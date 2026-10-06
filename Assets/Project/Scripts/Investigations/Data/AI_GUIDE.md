# Mapa — Investigations / Data

- **Caminho:** `Assets/Project/Scripts/Investigations/Data/`
- **Namespace:** `Fabula.PawOrder`
- **Propósito:** tipos de dados de autoria de um caso (`ScriptableObject`s, classes serializáveis e enums). Sem lógica de jogo.
- **Guia:** [InvestigationDataGuide.md](InvestigationDataGuide.md) · sistema: [InvestigationSystemGuide.md](../InvestigationSystemGuide.md)

## Arquivos

| Arquivo | Classe | Responsabilidade | API principal |
|---|---|---|---|
| `CaseData.cs` | `CaseData` (SO) | Definição de um caso | `StartingLocation`, `StartingUnlockedLocations`, `StartingPrompts`, `Suspects`, `AllLocations`, `AllCharacters`, `AllPrompts` |
| `CharacterData.cs` | `CharacterData` (SO) | Identidade de um personagem | `CharacterId`, `DisplayName`, `Portrait`, `IsSuspect`, `CanBeChosenAsCulprit`, `RoleTags` |
| `LocationData.cs` | `LocationData` (SO) | Um local do caso | `LocationId`, `CharactersPresent`, `CollectibleItems`, `TravelTargets`, `AvailableFromStart` |
| `InvestigationPromptData.cs` | `InvestigationPromptData` (SO abstrata) | Base de tudo que pode ser usado numa conversa | `PromptId`, `DisplayName`, `Description`, `PromptType`, `Icon`, `VisibleInPromptLists` |
| `ItemPromptData.cs` | `ItemPromptData` | Prompt de item coletável | `SourceLocation`, `InspectRadius`, `HoldTimeToCollect`, `InventoryCategory`, `Tags` |
| `QuestionPromptData.cs` | `QuestionPromptData` | Prompt de pergunta e sua posição no menu | `QuestionId`, `MenuGroup`, `MenuTargetType`, `MenuTargetId`, `MenuLabel`, `SortOrder`, `StartsVisible`, `IsFollowUpQuestion` |
| `ReferencePromptData.cs` | `ReferencePromptData` | Prompt de referência (personagem, local, fato) vindo da planilha | `SourceSheet`, `SourceRow`, `TopicCategory`, `RelatedCharacter`, `RelatedLocation`, `RelatedItem`, `StartsUnlocked` |
| `CharacterInteractionData.cs` | `CharacterInteractionData` (SO), `InteractionRequirements` | Personagem + prompt + requisitos → outcome | `TargetCharacter`, `PromptUsed`, `Outcome`, `Requirements`, `CanRepeat`, `ConsumeInteraction` |
| `InteractionOutcomeData.cs` | `InteractionOutcomeData` (SO) | Resultado de uma interação: resposta, prova e desbloqueios | `ResponseText`, `ProofSummary`, `CreatesUsableProof`, `UnlocksPrompts`, `UnlocksLocations`, `UnlocksSpecificInteractions`, `SuspectEvidenceValues` |
| `SuspectEvidenceValue.cs` | `SuspectEvidenceValue` | Peso de uma prova contra um suspeito | `Suspect`, `Score`, `MotivationScore`, `MeansScore`, `OpportunityScore` |
| `InvestigationPromptType.cs` | `InvestigationPromptType` (enum) | Tipo do prompt | `Item`, `Question`, `CharacterReference`, `LocationReference`, `EventReference`, `Clue` |
| `QuestionPromptMenuGroup.cs` | `QuestionPromptMenuGroup` (enum) | Grupo principal do menu de perguntas | `Character`, `Location`, `Item`, `Event` |
| `QuestionPromptMenuTargetType.cs` | `QuestionPromptMenuTargetType` (enum) | Tipo de alvo do submenu | `None`, `Character`, `Location`, `Item`, `Event` |
| `QuestionPromptAvailabilityState.cs` | `QuestionPromptAvailabilityState` (enum) | Estado de exibição de uma pergunta | `Hidden`, `LockedVisible`, `Available` |

## Assets criados a partir destes tipos

`Assets/Project/Scriptables/Investigations/PawOrder/` — `Cases`, `Characters`, `Locations`, `Items`, `Questions`, `Interactions/<Suspeito>`, `Outcomes/<Pergunta>`.
