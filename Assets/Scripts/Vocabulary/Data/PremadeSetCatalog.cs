using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace XRDiscovery.Vocabulary
{
    [Serializable]
    public sealed class PremadeSetDocument
    {
        // No default version: a missing version must fail validation.
        public int version;
        public string id;
        public string name;
        public string description;
        public int sortOrder;
        public string[] itemIds;
    }

    public sealed class PremadeSet
    {
        public VocabularySet Set { get; }
        public string Description { get; }
        public int SortOrder { get; }

        internal PremadeSet(PremadeSetDocument document)
        {
            Set = new VocabularySet(document.id, document.name, document.itemIds);
            Description = document.description ?? "";
            SortOrder = document.sortOrder;
        }
    }

    /// <summary>Read-only curated definitions. Categories never determine membership.</summary>
    public sealed class PremadeSetCatalog
    {
        public const string ResourcePath = "VocabularyData/PremadeSets";
        public IReadOnlyList<PremadeSet> Sets { get; }

        private PremadeSetCatalog(IEnumerable<PremadeSet> sets)
        {
            Sets = Array.AsReadOnly(sets.OrderBy(s => s.SortOrder)
                .ThenBy(s => s.Set.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Set.Id, StringComparer.Ordinal).ToArray());
        }

        public static PremadeSetCatalog Load(VocabularyLibrary library) => FromJson(
            Resources.LoadAll<TextAsset>(ResourcePath).Select(asset => asset.text), library.AllItemIds);

        public static PremadeSetCatalog FromJson(IEnumerable<string> documents, IEnumerable<string> availableIds)
        {
            var available = new HashSet<string>(availableIds, StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var sets = new List<PremadeSet>();
            foreach (string json in documents)
            {
                PremadeSetDocument document;
                try { document = JsonUtility.FromJson<PremadeSetDocument>(json); }
                catch (ArgumentException e) { throw new InvalidDataException("Invalid premade-set JSON.", e); }
                Validate(document, available);
                if (!ids.Add(document.id)) throw new InvalidDataException("Duplicate premade set ID: " + document.id);
                sets.Add(new PremadeSet(document));
            }
            return new PremadeSetCatalog(sets);
        }

        private static void Validate(PremadeSetDocument document, HashSet<string> available)
        {
            if (document == null || document.version != 1)
                throw new InvalidDataException("Premade set has an unsupported or missing version.");
            if (string.IsNullOrWhiteSpace(document.id) || !document.id.StartsWith("premade:", StringComparison.Ordinal)
                || document.id.Length == "premade:".Length || document.id == "premade:all")
                throw new InvalidDataException("Premade set needs a unique premade: ID; premade:all is reserved.");
            if (string.IsNullOrWhiteSpace(document.name) || document.itemIds == null || document.itemIds.Length == 0)
                throw new InvalidDataException("Premade set needs a name and at least one item: " + document.id);
            var members = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in document.itemIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !available.Contains(id))
                    throw new InvalidDataException("Premade set " + document.id + " references an unavailable item: " + id);
                if (!members.Add(id)) throw new InvalidDataException("Repeated item in premade set " + document.id + ": " + id);
            }
        }
    }
}
