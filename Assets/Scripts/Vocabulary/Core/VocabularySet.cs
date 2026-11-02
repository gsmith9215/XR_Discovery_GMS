using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace XRDiscovery.Vocabulary
{
    /// <summary>A snapshot of a named set; identity never depends on its display name.</summary>
    public sealed class VocabularySet
    {
        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<string> ItemIds { get; }

        public VocabularySet(string id, string name, IEnumerable<string> itemIds)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A vocabulary set needs an ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Enter a name for the vocabulary set.", nameof(name));

            Id = id;
            Name = name.Trim();
            ItemIds = new ReadOnlyCollection<string>(CopyDistinctIds(itemIds));
        }

        internal static List<string> CopyDistinctIds(IEnumerable<string> ids)
        {
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                if (string.IsNullOrWhiteSpace(id))
                    throw new ArgumentException("Vocabulary item IDs cannot be empty.", nameof(ids));
                if (seen.Add(id))
                    result.Add(id);
            }
            return result;
        }
    }
}
