using System;
using UnityEngine;

namespace Fabula.PawOrder
{
    [Serializable]
    public sealed class SuspectEvidenceValue
    {
        [SerializeField] private CharacterData suspect;
        [SerializeField] private int score;
        [SerializeField] private int motivationScore;
        [SerializeField] private int meansScore;
        [SerializeField] private int opportunityScore;

        public CharacterData Suspect => suspect;
        public int Score => score;
        public int MotivationScore => motivationScore;
        public int MeansScore => meansScore;
        public int OpportunityScore => opportunityScore;
    }
}
