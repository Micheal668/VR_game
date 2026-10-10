using System;
using UnityEngine;

namespace LunarEscape
{
    public sealed class StationRandomSupplies : MonoBehaviour
    {
        [Serializable] public sealed class Group { public string name; public CargoItem[] items; }
        [SerializeField] private LifeSupportMission life;
        [SerializeField] private Group[] groups = Array.Empty<Group>();
        private int lastAttempt = -1;
        private System.Random random;
        public Group[] Groups => groups;
        public void Configure(LifeSupportMission source, Group[] sets) { life = source; groups = sets; }
        public void SetRandomSeed(int seed) => random = new System.Random(seed);
        private void Start() => RefreshAttempt();
        private void LateUpdate() => RefreshAttempt();
        private void RefreshAttempt()
        {
            if (life == null || lastAttempt == life.AttemptNumber) return;
            lastAttempt = life.AttemptNumber; random ??= new System.Random();
            foreach (var group in groups)
            {
                int count = random.Next(1, Mathf.Min(3, group.items.Length) + 1);
                for (int i = 0; i < group.items.Length; i++) group.items[i].gameObject.SetActive(i < count);
            }
        }
    }
}
