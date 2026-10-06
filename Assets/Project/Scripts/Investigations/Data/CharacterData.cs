using System.Collections.Generic;
using UnityEngine;

namespace Fabula.PawOrder
{
    [CreateAssetMenu(
        fileName = "CharacterData",
        menuName = "Fabula/Paw Order/Investigations/Character Data")]
    public sealed class CharacterData : ScriptableObject
    {
        [SerializeField] private string characterId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite portrait;
        [SerializeField] private bool isSuspect;
        [SerializeField] private bool canBeChosenAsCulprit;
        [SerializeField] private List<string> roleTags = new();

        public string CharacterId => characterId;
        public string DisplayName => displayName;
        public Sprite Portrait => portrait;
        public bool IsSuspect => isSuspect;
        public bool CanBeChosenAsCulprit => canBeChosenAsCulprit;
        public IReadOnlyList<string> RoleTags => roleTags;
    }
}
