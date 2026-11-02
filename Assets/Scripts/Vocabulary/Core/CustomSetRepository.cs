using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using UnityEngine;

namespace XRDiscovery.Vocabulary
{
    /// <summary>
    /// Stores custom sets by stable IDs, independently of names and premade categories.
    /// Successful mutations are saved before becoming visible in memory. Unknown item
    /// IDs are retained so a temporarily missing asset does not erase a player's choices.
    /// </summary>
    public sealed class CustomSetRepository
    {
        private const int SchemaVersion = 1;
        private const string Format = "XRDiscovery.Vocabulary.CustomSets";
        private readonly string filePath;
        private List<VocabularySet> sets = new List<VocabularySet>();
        private bool loaded;
        private bool recoveredFromBackup;
        private string loadedPrimaryText;

        public string FilePath => filePath;
        public string RecoveryNotice { get; private set; }
        public IReadOnlyList<VocabularySet> Sets
        {
            get
            {
                EnsureLoaded();
                return Snapshot(sets);
            }
        }

        public CustomSetRepository(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("A custom-set save path is required.", nameof(filePath));
            this.filePath = Path.GetFullPath(filePath);
        }

        public IReadOnlyList<VocabularySet> Load()
        {
            // Failed reloads must block later saves, even if this instance loaded before.
            loaded = false;
            recoveredFromBackup = false;
            RecoveryNotice = null;
            bool primaryExists = File.Exists(filePath);
            bool backupExists = File.Exists(BackupPath);
            loadedPrimaryText = primaryExists ? File.ReadAllText(filePath, Encoding.UTF8) : null;
            if (!primaryExists && !backupExists)
            {
                sets = new List<VocabularySet>();
                loaded = true;
                return Snapshot(sets);
            }

            InvalidDataException primaryError = null;
            if (primaryExists)
            {
                try
                {
                    sets = ReadDocument(loadedPrimaryText);
                    loaded = true;
                    return Snapshot(sets);
                }
                catch (InvalidDataException exception)
                {
                    primaryError = exception;
                }
            }

            if (backupExists)
            {
                try
                {
                    sets = ReadDocument(File.ReadAllText(BackupPath, Encoding.UTF8));
                    recoveredFromBackup = true;
                    RecoveryNotice = "Custom sets were recovered from the previous save. " +
                        "The damaged save will be preserved when you next save changes.";
                    loaded = true;
                    return Snapshot(sets);
                }
                catch (InvalidDataException backupError)
                {
                    throw new InvalidDataException("The custom-set save and its backup could not be read. " +
                        "Both files have been left unchanged.", primaryError ?? backupError);
                }
            }

            throw new InvalidDataException("The custom-set save could not be read and no backup is available. " +
                "The file has been left unchanged.", primaryError);
        }

        public VocabularySet Create(string name, IEnumerable<string> itemIds)
        {
            EnsureLoaded();
            var created = new VocabularySet("custom:" + Guid.NewGuid().ToString("N"), name, itemIds);
            var next = new List<VocabularySet>(sets) { created };
            Commit(next);
            return Copy(created);
        }

        public VocabularySet Update(string id, string name, IEnumerable<string> itemIds)
        {
            EnsureLoaded();
            int index = FindIndex(id);
            var updated = new VocabularySet(sets[index].Id, name, itemIds);
            var next = new List<VocabularySet>(sets);
            next[index] = updated;
            Commit(next);
            return Copy(updated);
        }

        public VocabularySet Duplicate(string id, string newName)
        {
            EnsureLoaded();
            return Create(newName, sets[FindIndex(id)].ItemIds);
        }

        public void Delete(string id)
        {
            EnsureLoaded();
            var next = new List<VocabularySet>(sets);
            next.RemoveAt(FindIndex(id));
            Commit(next);
        }

        public void Save()
        {
            EnsureLoaded();
            Commit(new List<VocabularySet>(sets));
        }

        private string BackupPath => filePath + ".bak";

        private void EnsureLoaded()
        {
            if (!loaded)
                Load();
        }

        private int FindIndex(string id)
        {
            int index = sets.FindIndex(set => string.Equals(set.Id, id, StringComparison.Ordinal));
            if (index < 0)
                throw new KeyNotFoundException("This custom vocabulary set no longer exists.");
            return index;
        }

        private void Commit(List<VocabularySet> next)
        {
            string currentText = File.Exists(filePath) ? File.ReadAllText(filePath, Encoding.UTF8) : null;
            if (!string.Equals(currentText, loadedPrimaryText, StringComparison.Ordinal))
            {
                loaded = false;
                throw new IOException("The custom-set save changed outside this session. " +
                    "Reload your custom sets before saving again; the existing file has been left unchanged.");
            }

            string directory = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(directory);
            string temporaryPath = filePath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                string json = Serialize(next);
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                           FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (recoveredFromBackup)
                {
                    // Never replace the good recovery backup with the corrupt primary.
                    if (File.Exists(filePath))
                        File.Move(filePath, filePath + ".corrupt-" +
                            DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N"));
                    File.Move(temporaryPath, filePath);
                }
                else if (File.Exists(filePath))
                {
                    File.Replace(temporaryPath, filePath, BackupPath);
                }
                else
                {
                    File.Move(temporaryPath, filePath);
                }

                sets = next;
                loadedPrimaryText = json;
                recoveredFromBackup = false;
                RecoveryNotice = null;
            }
            finally
            {
                // A failed write must not change the in-memory collection. Temporary-file
                // cleanup is best effort so it cannot turn a successful commit into a failure.
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static string Serialize(List<VocabularySet> source)
        {
            var document = new SaveDocument
            {
                format = Format,
                version = SchemaVersion,
                sets = new SavedSet[source.Count]
            };
            for (int i = 0; i < source.Count; i++)
            {
                document.sets[i] = new SavedSet
                {
                    id = source[i].Id,
                    name = source[i].Name,
                    itemIds = new List<string>(source[i].ItemIds).ToArray()
                };
            }
            return JsonUtility.ToJson(document, true);
        }

        private static List<VocabularySet> ReadDocument(string json)
        {
            SaveDocument document;
            try
            {
                document = JsonUtility.FromJson<SaveDocument>(json);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("The custom-set save is not valid JSON.", exception);
            }

            if (document == null || document.format != Format || document.sets == null)
                throw new InvalidDataException("The custom-set save has missing or invalid fields.");
            if (document.version != SchemaVersion)
                throw new NotSupportedException("This custom-set save uses an unsupported version (" +
                    document.version + "). It has been left unchanged.");

            var result = new List<VocabularySet>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (SavedSet saved in document.sets)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.id) ||
                    !saved.id.StartsWith("custom:", StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(saved.name) || saved.itemIds == null || !ids.Add(saved.id))
                    throw new InvalidDataException("The custom-set save contains an invalid or duplicate set.");
                try
                {
                    result.Add(new VocabularySet(saved.id, saved.name, saved.itemIds));
                }
                catch (ArgumentException exception)
                {
                    throw new InvalidDataException("The custom-set save contains invalid vocabulary IDs.", exception);
                }
            }
            return result;
        }

        private static VocabularySet Copy(VocabularySet source) =>
            new VocabularySet(source.Id, source.Name, source.ItemIds);

        private static IReadOnlyList<VocabularySet> Snapshot(List<VocabularySet> source)
        {
            var copies = new List<VocabularySet>(source.Count);
            foreach (VocabularySet set in source)
                copies.Add(Copy(set));
            return new ReadOnlyCollection<VocabularySet>(copies);
        }

        [Serializable]
        private sealed class SaveDocument
        {
            public string format;
            public int version;
            public SavedSet[] sets;
        }

        [Serializable]
        private sealed class SavedSet
        {
            public string id;
            public string name;
            public string[] itemIds;
        }
    }
}
