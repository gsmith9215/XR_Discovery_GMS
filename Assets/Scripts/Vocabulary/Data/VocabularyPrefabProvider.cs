using System.Collections.Generic;
using UnityEngine;

namespace XRDiscovery.Vocabulary
{
    public sealed class VocabularyPrefabProvider
    {
        private readonly Dictionary<string, GameObject> cache = new();

        public GameObject Load(VocabularyEntry entry)
        {
            if (!cache.TryGetValue(entry.ResourcePath, out var prefab) || prefab == null)
                cache[entry.ResourcePath] = prefab = Resources.Load<GameObject>(entry.ResourcePath);
            return prefab;
        }
    }
}
