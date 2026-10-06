using System.Collections.Generic;
using UnityEngine;

namespace Iung.Tools.Shared.Collections
{
    // AI GUIDANCE:
    // - When to use: when working with List random picks, null cleanup, safe last-item access, or bulk destroy helpers.
    // - Prefer: using existing ListUtils extensions instead of duplicating random selection or list hygiene logic in feature scripts.
    // - Avoid: reimplementing equivalent helper methods in gameplay classes when ListUtils already provides the behavior.
    // - Reason: GetRandom and RemoveRandom assume a non-empty list, so callers should validate list count before calling.
    public static class ListUtils
    {
        public static T GetRandom<T>(this List<T> list)
        {
            return list[Random.Range(0, list.Count)];
        }

        public static T RemoveRandom<T>(this List<T> list)
        {
            int index = Random.Range(0, list.Count);
            T item = list[index];
            list.RemoveAt(index);
            return item;
        }

        public static T GetRandomExcluding<T>(this List<T> list, HashSet<T> exclusion)
        {
            if (list.Count == 0 || exclusion == null || exclusion.Count >= list.Count) return default;

            T item;
            int attempts = 0;
            do
            {
                item = list[Random.Range(0, list.Count)];
                attempts++;
            }
            while (exclusion.Contains(item) && attempts < 100);

            return item;
        }

        public static T GetRandomExcluding<T>(this List<T> list, List<T> exclusionList)
        {
            if (list.Count == 0 || exclusionList == null || exclusionList.Count >= list.Count) return default;
            var exclusionSet = new HashSet<T>(exclusionList);
            return list.GetRandomExcluding(exclusionSet);
        }

        public static void DestroyGameObjects<T>(this List<T> list)
        {
            foreach (var item in list)
            {
                if (item is GameObject go)
                {
                    Object.Destroy(go);
                }
            }
        }

        public static void RemoveNulls<T>(this List<T> list)
        {
            list.RemoveAll(static x => x == null);
        }

        public static T GetLast<T>(this List<T> list)
        {
            return list.Count > 0 ? list[^1] : default;
        }

        public static void LogAll<T>(this List<T> list)
        {
            foreach (var item in list)
            {
                Debug.Log(item);
            }
        }
    }
}
