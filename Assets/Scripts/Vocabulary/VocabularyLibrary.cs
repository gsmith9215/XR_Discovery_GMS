using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace XRDiscovery.Vocabulary
{
    [Serializable]
    public sealed class VocabularyEntry
    {
        [SerializeField] private string id;
        [SerializeField] private string name;
        [SerializeField] private string japanese;
        [SerializeField] private string reading;
        [SerializeField] private string category;
        [SerializeField] private string resourcePath;
        [NonSerialized] private GameObject prefab;

        public string Id => id;
        public string Name => name;
        public string Japanese => japanese ?? string.Empty;
        public string Reading => reading ?? string.Empty;
        public string Category => category;
        public string ResourcePath => resourcePath;
        public string Prompt => string.IsNullOrWhiteSpace(Japanese) ? Name : Japanese;
        public GameObject Prefab => LoadPrefab();

        public VocabularyEntry(string id, string name, string japanese, string reading,
            string category, string resourcePath)
        {
            this.id = id;
            this.name = name;
            this.japanese = japanese;
            this.reading = reading;
            this.category = category;
            this.resourcePath = resourcePath;
        }

        public GameObject LoadPrefab()
        {
            if (prefab == null)
                prefab = Resources.Load<GameObject>(resourcePath);
            return prefab;
        }
    }

    [Serializable]
    public sealed class VocabularyLibraryData
    {
        public int version = 1;
        public List<VocabularyEntry> entries = new List<VocabularyEntry>();
    }

    /// <summary>Build-generated vocabulary metadata. Prefab GUIDs survive renames and folder moves.</summary>
    public sealed class VocabularyLibrary
    {
        public const string CatalogResourcePath = "VocabularyData/Library";
        public const string FontResourcePath = "VocabularyData/JapaneseFont";

        private readonly Dictionary<string, VocabularyEntry> byId;
        private readonly Dictionary<string, IReadOnlyList<VocabularyEntry>> byCategory;
        public IReadOnlyList<VocabularyEntry> Entries { get; }
        public IReadOnlyList<string> Categories { get; }
        public IReadOnlyList<string> AllItemIds { get; }

        private VocabularyLibrary(VocabularyLibraryData data)
        {
            if (data == null || data.version != 1 || data.entries == null)
                throw new InvalidOperationException("Vocabulary library has an unsupported or invalid format.");

            byId = new Dictionary<string, VocabularyEntry>(StringComparer.Ordinal);
            foreach (var entry in data.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) ||
                    string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.Category) ||
                    string.IsNullOrWhiteSpace(entry.ResourcePath) ||
                    !entry.ResourcePath.StartsWith("VocabularyBoxes/", StringComparison.Ordinal))
                    throw new InvalidOperationException("Vocabulary library contains an incomplete entry.");
                if (!byId.TryAdd(entry.Id, entry))
                    throw new InvalidOperationException("Vocabulary library contains a duplicate item ID: " + entry.Id);
            }

            Entries = new ReadOnlyCollection<VocabularyEntry>(byId.Values
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Id).ToList());
            Categories = new ReadOnlyCollection<string>(Entries.Select(e => e.Category).Distinct()
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList());
            AllItemIds = new ReadOnlyCollection<string>(Entries.Select(e => e.Id).ToList());
            byCategory = Categories.ToDictionary(c => c,
                c => (IReadOnlyList<VocabularyEntry>)new ReadOnlyCollection<VocabularyEntry>(
                    Entries.Where(e => e.Category == c).ToList()), StringComparer.Ordinal);
        }

        public static VocabularyLibrary Load()
        {
            var asset = Resources.Load<TextAsset>(CatalogResourcePath);
            if (asset == null)
                throw new InvalidOperationException("Vocabulary library is missing. Use Tools > Vocabulary > Rebuild Library in Unity.");
            return new VocabularyLibrary(JsonUtility.FromJson<VocabularyLibraryData>(asset.text));
        }

        public VocabularyEntry Get(string id) => id != null && byId.TryGetValue(id, out var item) ? item : null;

        public IReadOnlyList<VocabularyEntry> GetCategory(string category) =>
            category != null && byCategory.TryGetValue(category, out var items)
                ? items : Array.Empty<VocabularyEntry>();
    }
}
