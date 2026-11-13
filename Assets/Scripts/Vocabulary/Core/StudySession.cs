using System;
using System.Collections.Generic;

namespace XRDiscovery.Vocabulary
{
    public sealed class StudySession
    {
        public IReadOnlyList<string> ItemIds { get; }
        public int Index { get; private set; }
        public string CurrentItemId => ItemIds[Index];

        public StudySession(IEnumerable<string> ids)
        {
            ItemIds = VocabularySet.CopyDistinctIds(ids).AsReadOnly();
            if (ItemIds.Count == 0) throw new ArgumentException("Choose at least one available item.", nameof(ids));
        }

        public void Move(int delta) => Index = (Index + delta % ItemIds.Count + ItemIds.Count) % ItemIds.Count;
    }
}
