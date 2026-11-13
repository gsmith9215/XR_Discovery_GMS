using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using XRDiscovery.Vocabulary;

/// <summary>The editor writes the same JSON that the runtime loads; there is no generated second copy.</summary>
public static class PremadeSetAuthoring
{
    public const string Folder = "Assets/Resources/VocabularyData/PremadeSets";

    public static string[] Files() => Directory.Exists(Folder)
        ? Directory.GetFiles(Folder, "*.json", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal).ToArray()
        : Array.Empty<string>();

    public static PremadeSetDocument Read(string path)
    {
        var document = JsonUtility.FromJson<PremadeSetDocument>(File.ReadAllText(path));
        if (document == null) throw new InvalidDataException("Empty premade set: " + path);
        return document;
    }

    [MenuItem("Tools/Vocabulary/Validate Premade Sets")]
    public static void Validate()
    {
        var library = VocabularyLibrary.Load();
        var catalog = PremadeSetCatalog.FromJson(Files().Select(File.ReadAllText), library.AllItemIds);
        foreach (var entry in library.Entries)
        {
            string path = "Assets/Resources/" + entry.ResourcePath + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                throw new InvalidDataException("Catalog item has no prefab: " + entry.Name + " (" + path + ")");
        }
        Debug.Log($"Validated {catalog.Sets.Count} premade sets against {library.Entries.Count} vocabulary items.");
    }

    public static void Save(string path, PremadeSetDocument document, VocabularyLibrary library)
    {
        string fullPath = Path.GetFullPath(path);
        string folder = Path.GetFullPath(Folder) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(folder, StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path) != ".json")
            throw new InvalidOperationException("Premade sets must be JSON files inside " + Folder);
        string json = JsonUtility.ToJson(document, true) + "\n";
        var otherFiles = Files().Where(p => !string.Equals(Path.GetFullPath(p), fullPath, StringComparison.OrdinalIgnoreCase));
        PremadeSetCatalog.FromJson(otherFiles.Select(File.ReadAllText).Concat(new[] { json }), library.AllItemIds);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        // Validate first, then replace the complete document without truncating an existing definition.
        string temporary = fullPath + ".tmp";
        try
        {
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
            else File.Move(temporary, fullPath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    public static IEnumerable<string> FontText() => Files().Select(path =>
    {
        var document = Read(path);
        return document.name + document.description;
    });
}
