using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace XRDiscovery.Vocabulary.Tests
{
    public sealed class PremadeSetTests
    {
        private static string Json(string id = "premade:lesson", string[] items = null, int version = 1) =>
            JsonUtility.ToJson(new PremadeSetDocument
            {
                version = version, id = id, name = "Lesson", description = "Test lesson",
                itemIds = items ?? new[] { "b", "a" }
            });

        [Test]
        public void CuratedMembershipAndOrderDoNotExpandWhenCatalogGrows()
        {
            string document = Json();
            var first = PremadeSetCatalog.FromJson(new[] { document }, new[] { "a", "b" });
            var expanded = PremadeSetCatalog.FromJson(new[] { document }, new[] { "a", "b", "c" });
            Assert.That(first.Sets[0].Set.ItemIds, Is.EqualTo(new[] { "b", "a" }));
            Assert.That(expanded.Sets[0].Set.ItemIds, Is.EqualTo(first.Sets[0].Set.ItemIds));
            var session = new StudySession(expanded.Sets[0].Set.ItemIds);
            Assert.That(session.CurrentItemId, Is.EqualTo("b"));
            session.Move(-1);
            Assert.That(session.CurrentItemId, Is.EqualTo("a"));
            session.Move(3);
            Assert.That(session.CurrentItemId, Is.EqualTo("b"));
        }

        [Test]
        public void DuplicateSetIdsAndUnknownOrRepeatedMembersAreRejected()
        {
            string[] available = { "a", "b" };
            Assert.Throws<InvalidDataException>(() => PremadeSetCatalog.FromJson(new[] { Json(), Json() }, available));
            Assert.Throws<InvalidDataException>(() => PremadeSetCatalog.FromJson(new[] { Json(items: new[] { "missing" }) }, available));
            Assert.Throws<InvalidDataException>(() => PremadeSetCatalog.FromJson(new[] { Json(items: new[] { "a", "a" }) }, available));
            Assert.Throws<InvalidDataException>(() => PremadeSetCatalog.FromJson(new[] { Json(items: Array.Empty<string>()) }, available));
        }

        [TestCase(0)]
        [TestCase(2)]
        public void UnsupportedOrMissingVersionIsRejected(int version) =>
            Assert.Throws<InvalidDataException>(() => PremadeSetCatalog.FromJson(new[] { Json(version: version) }, new[] { "a", "b" }));

        [TestCase("premade:all")]
        [TestCase("custom:lesson")]
        [TestCase("premade:")]
        public void ReservedAndInvalidSetIdsAreRejected(string id) =>
            Assert.Throws<InvalidDataException>(() => PremadeSetCatalog.FromJson(new[] { Json(id) }, new[] { "a", "b" }));

        [Test]
        public void ShippedDefinitionsResolveToRealPrefabs()
        {
            var library = VocabularyLibrary.Load();
            var catalog = PremadeSetCatalog.Load(library);
            Assert.That(catalog.Sets, Is.Not.Empty);
            foreach (var set in catalog.Sets)
                foreach (string id in set.Set.ItemIds)
                    Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/" + library.Get(id).ResourcePath + ".prefab"),
                        Is.Not.Null, set.Set.Name + ": " + id);
        }

        [Test]
        public void CopyingPremadeSetCreatesIndependentCustomMembership()
        {
            string folder = Path.Combine(Path.GetTempPath(), "PremadeCopy-" + Guid.NewGuid().ToString("N"));
            try
            {
                var original = PremadeSetCatalog.FromJson(new[] { Json() }, new[] { "a", "b", "c" }).Sets[0].Set;
                var repository = new CustomSetRepository(Path.Combine(folder, "custom-sets.json"));
                repository.Load();
                var copy = repository.Create(original.Name, original.ItemIds);
                repository.Update(copy.Id, "My lesson", new[] { "c" });
                Assert.That(original.ItemIds, Is.EqualTo(new[] { "b", "a" }));
                var revised = PremadeSetCatalog.FromJson(new[] { Json(items: new[] { "a" }) }, new[] { "a", "b", "c" });
                Assert.That(new CustomSetRepository(repository.FilePath).Load().Single().ItemIds, Is.EqualTo(new[] { "c" }));
                Assert.That(revised.Sets[0].Set.ItemIds, Is.EqualTo(new[] { "a" }));
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }

        [Test]
        public void MainSceneHasExplicitStartupAndPreservedPresenterReferences()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/SampleScene.unity");
            try
            {
                var apps = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<VocabularyApp>(true)).ToArray();
                Assert.That(apps.Length, Is.EqualTo(1));
                var app = new SerializedObject(apps[0]);
                var presenter = app.FindProperty("presenter").objectReferenceValue as VocabularyObjectPresenter;
                Assert.That(presenter, Is.Not.Null);
                Assert.That(app.FindProperty("hammer").objectReferenceValue, Is.Not.Null);
                Assert.That(presenter.PlayerCamera, Is.Not.Null);
                Assert.That(presenter.RightHandAnchor, Is.Not.Null);
                var hammer = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<HammerHitLogger>(true)).Single();
                Assert.That(new SerializedObject(hammer).FindProperty("spawner").objectReferenceValue, Is.SameAs(presenter));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
