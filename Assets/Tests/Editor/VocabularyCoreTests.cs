using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace XRDiscovery.Vocabulary.Tests
{
    public sealed class VocabularyCoreTests
    {
        private string testDirectory;
        private string savePath;

        [SetUp]
        public void SetUp()
        {
            testDirectory = Path.Combine(Path.GetTempPath(), "XRDiscovery-VocabularyTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            savePath = Path.Combine(testDirectory, "custom-sets.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testDirectory))
                Directory.Delete(testDirectory, true);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(182)]
        public void EverySelectedTargetIsAskedExactlyOnceIncludingRemainders(int count)
        {
            string[] targets = Enumerable.Range(0, count).Select(i => "item-" + i).ToArray();
            string[] catalog = targets.Concat(new[] { "extra-1", "extra-2" }).ToArray();
            var session = new TestSession(targets, catalog, new Random(17));
            var seen = new List<string>();
            while (!session.IsRoundComplete)
            {
                TestQuestion question = session.CurrentQuestion;
                Assert.That(question.ChoiceIds.Count, Is.EqualTo(3));
                Assert.That(question.ChoiceIds.Distinct().Count(), Is.EqualTo(3));
                Assert.That(question.ChoiceIds, Does.Contain(question.TargetId));
                Assert.That(question.ChoiceIds.All(catalog.Contains), Is.True);
                seen.Add(question.TargetId);
                Assert.That(session.SubmitAnswer(question.Sequence, question.TargetId), Is.True);
            }
            Assert.That(seen, Is.EquivalentTo(targets));
            Assert.That(session.AnsweredCount, Is.EqualTo(count));
            Assert.That(session.CorrectCount, Is.EqualTo(count));
            Assert.That(session.MissedItemIds, Is.Empty);
            Assert.That(session.StartNextMissedRound(), Is.False);
        }

        [Test]
        public void MissedRoundsShrinkUntilMasteredAndFullRetestRestoresOriginalSet()
        {
            string[] targets = { "a", "b", "c", "d", "e" };
            var session = new TestSession(targets, targets, new Random(41));
            CompleteRound(session, new[] { "a", "c", "e" });
            Assert.That(session.MissedItemIds, Is.EquivalentTo(new[] { "a", "c", "e" }));
            Assert.That(session.CorrectCount, Is.EqualTo(2));
            Assert.That(session.StartNextMissedRound(), Is.True);
            Assert.That(session.RoundNumber, Is.EqualTo(2));
            Assert.That(session.RoundItemIds, Is.EquivalentTo(new[] { "a", "c", "e" }));
            Assert.That(session.AnsweredCount, Is.Zero);
            CompleteRound(session, new[] { "c" });
            Assert.That(session.StartNextMissedRound(), Is.True);
            Assert.That(session.RoundNumber, Is.EqualTo(3));
            Assert.That(session.RoundItemIds, Is.EquivalentTo(new[] { "c" }));
            Assert.That(session.CurrentQuestion.ChoiceIds.Count, Is.EqualTo(3));
            CompleteRound(session, Array.Empty<string>());
            Assert.That(session.StartNextMissedRound(), Is.False);
            session.Retest();
            Assert.That(session.RoundNumber, Is.EqualTo(1));
            Assert.That(session.RoundItemIds, Is.EquivalentTo(targets));
            Assert.That(session.CorrectCount, Is.Zero);
            Assert.That(session.MissedItemIds, Is.Empty);
        }

        [Test]
        public void DistractorsPreferSelectedSetAndNeverBecomeAdditionalTargets()
        {
            string[] targets = { "apple", "pear", "peach" };
            var session = new TestSession(targets, targets.Concat(new[] { "truck", "tree" }), new Random(1));
            while (!session.IsRoundComplete)
            {
                TestQuestion question = session.CurrentQuestion;
                Assert.That(question.ChoiceIds, Is.EquivalentTo(targets));
                session.SubmitAnswer(question.Sequence, question.TargetId);
            }
            Assert.That(session.AnsweredCount, Is.EqualTo(3));
        }

        [Test]
        public void DuplicateAnswerLabelsCannotMakeAQuestionAmbiguous()
        {
            string[] all = { "hat", "cap", "apple", "chair", "stool" };
            var labels = new Dictionary<string, string>
            {
                { "hat", "帽子" }, { "cap", " 帽子 " }, { "apple", "りんご" },
                { "chair", "椅子" }, { "stool", "椅子" }
            };
            var session = new TestSession(new[] { "hat", "cap" }, all, new Random(9), id => labels[id]);
            while (!session.IsRoundComplete)
            {
                TestQuestion question = session.CurrentQuestion;
                Assert.That(question.ChoiceIds.Count, Is.EqualTo(3));
                Assert.That(question.ChoiceIds.Select(id => labels[id].Trim()).Distinct().Count(), Is.EqualTo(3));
                session.SubmitAnswer(question.Sequence, question.TargetId);
            }
            Assert.That(session.AnsweredCount, Is.EqualTo(2));
        }

        [Test]
        public void SmallCatalogUsesOnlyAvailableDistinctAnswers()
        {
            var session = new TestSession(new[] { "a" }, new[] { "a", "b" }, new Random(1));
            Assert.That(session.CurrentQuestion.ChoiceIds, Is.EquivalentTo(new[] { "a", "b" }));
            var synonymOnly = new TestSession(new[] { "a" }, new[] { "a", "b" }, new Random(1), _ => "same");
            Assert.That(synonymOnly.CurrentQuestion.ChoiceIds, Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public void StaleAndInvalidAnswersCannotAdvanceOrScoreAnotherQuestion()
        {
            var session = new TestSession(new[] { "a", "b", "c" }, new[] { "a", "b", "c" }, new Random(7));
            TestQuestion first = session.CurrentQuestion;
            Assert.Throws<ArgumentException>(() => session.SubmitAnswer(first.Sequence, "unknown"));
            Assert.That(session.AnsweredCount, Is.Zero);
            session.SubmitAnswer(first.Sequence, first.TargetId);
            Assert.Throws<InvalidOperationException>(() => session.SubmitAnswer(first.Sequence, first.TargetId));
            Assert.That(session.AnsweredCount, Is.EqualTo(1));
            Assert.That(session.CorrectCount, Is.EqualTo(1));
            session.Retest();
            Assert.Throws<InvalidOperationException>(() => session.SubmitAnswer(first.Sequence, first.TargetId));
            Assert.That(session.AnsweredCount, Is.Zero);
        }

        [Test]
        public void CompletedRoundRejectsFurtherAnswersAndUnfinishedRoundRejectsContinuation()
        {
            var session = new TestSession(new[] { "a" }, new[] { "a", "b", "c" });
            Assert.Throws<InvalidOperationException>(() => session.StartNextMissedRound());
            TestQuestion question = session.CurrentQuestion;
            session.SubmitAnswer(question.Sequence, question.TargetId);
            Assert.Throws<InvalidOperationException>(() => session.SubmitAnswer(question.Sequence, question.TargetId));
            Assert.That(session.CorrectCount, Is.EqualTo(1));
        }

        [Test]
        public void TestTakesASnapshotAndDeduplicatesSelectedIds()
        {
            var selected = new List<string> { "a", "a", "b" };
            var session = new TestSession(selected, new[] { "a", "a", "b", "c" });
            selected.Clear();
            Assert.That(session.OriginalItemIds, Is.EqualTo(new[] { "a", "b" }));
            CompleteRound(session, Array.Empty<string>());
            Assert.That(session.AnsweredCount, Is.EqualTo(2));
        }

        [Test]
        public void EmptyOrMissingTargetsFailBeforeTestStarts()
        {
            Assert.Throws<ArgumentException>(() => new TestSession(Array.Empty<string>(), new[] { "a" }));
            Assert.Throws<ArgumentException>(() => new TestSession(new[] { "missing" }, new[] { "a" }));
            Assert.Throws<ArgumentException>(() => new TestSession(new[] { " " }, new[] { "a" }));
        }

        [Test]
        public void NewRepositoryIsEmptyWithoutCreatingASave()
        {
            var repository = new CustomSetRepository(savePath);
            Assert.That(repository.Load(), Is.Empty);
            Assert.That(File.Exists(savePath), Is.False);
        }

        [Test]
        public void SameNamesUseDifferentIdsAndSurviveRestartWithUnicodeAndUnknownItems()
        {
            var repository = new CustomSetRepository(savePath);
            VocabularySet first = repository.Create(" 食べ物 / Food ", new[] { "apple", "removed-for-now" });
            VocabularySet second = repository.Create("食べ物 / Food", new[] { "pear" });
            Assert.That(first.Id, Is.Not.EqualTo(second.Id));
            Assert.That(first.Name, Is.EqualTo(second.Name));
            IReadOnlyList<VocabularySet> reloaded = new CustomSetRepository(savePath).Load();
            Assert.That(reloaded.Count, Is.EqualTo(2));
            Assert.That(reloaded[0].ItemIds, Is.EqualTo(new[] { "apple", "removed-for-now" }));
            Assert.That(reloaded[0].Id, Is.EqualTo(first.Id));
            Assert.That(reloaded[1].Name, Is.EqualTo("食べ物 / Food"));
        }

        [Test]
        public void TemplatesAndDuplicatesAreIndependentCopies()
        {
            var templateItems = new List<string> { "apple", "pear" };
            var premade = new VocabularySet("premade:Food", "Food", templateItems);
            var repository = new CustomSetRepository(savePath);
            VocabularySet custom = repository.Create(premade.Name, premade.ItemIds);
            VocabularySet duplicate = repository.Duplicate(custom.Id, "Food copy");
            templateItems.Clear();
            repository.Update(custom.Id, "Renamed", new[] { "pear", "peach" });
            Assert.That(premade.ItemIds, Is.EqualTo(new[] { "apple", "pear" }));
            Assert.That(duplicate.ItemIds, Is.EqualTo(new[] { "apple", "pear" }));
            Assert.That(custom.Name, Is.EqualTo("Food"));
            IReadOnlyList<VocabularySet> saved = new CustomSetRepository(savePath).Load();
            Assert.That(saved.Single(set => set.Id == duplicate.Id).Name, Is.EqualTo("Food copy"));
            Assert.That(saved.Single(set => set.Id == custom.Id).ItemIds, Is.EqualTo(new[] { "pear", "peach" }));
        }

        [Test]
        public void EmptySetsCanBeSavedAndEditedButCannotStartATest()
        {
            var repository = new CustomSetRepository(savePath);
            VocabularySet empty = repository.Create("Later", Array.Empty<string>());
            Assert.That(new CustomSetRepository(savePath).Load()[0].ItemIds, Is.Empty);
            Assert.Throws<ArgumentException>(() => new TestSession(empty.ItemIds, new[] { "a" }));
            VocabularySet updated = repository.Update(empty.Id, "Ready", new[] { "a", "a", "b" });
            Assert.That(updated.ItemIds, Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void DeleteAndRenamePersistWithoutChangingOtherSets()
        {
            var repository = new CustomSetRepository(savePath);
            VocabularySet first = repository.Create("Food", new[] { "apple" });
            VocabularySet second = repository.Create("Food", new[] { "pear" });
            repository.Update(second.Id, "Pears", second.ItemIds);
            repository.Delete(first.Id);
            IReadOnlyList<VocabularySet> saved = new CustomSetRepository(savePath).Load();
            Assert.That(saved.Count, Is.EqualTo(1));
            Assert.That(saved[0].Id, Is.EqualTo(second.Id));
            Assert.That(saved[0].Name, Is.EqualTo("Pears"));
            Assert.Throws<KeyNotFoundException>(() => repository.Delete(first.Id));
            Assert.That(repository.Sets.Count, Is.EqualTo(1));
        }

        [Test]
        public void InvalidMutationPreservesSaveAndMemory()
        {
            var repository = new CustomSetRepository(savePath);
            VocabularySet original = repository.Create("Food", new[] { "apple" });
            string before = File.ReadAllText(savePath);
            Assert.Throws<ArgumentException>(() => repository.Update(original.Id, "  ", new[] { "pear" }));
            Assert.Throws<ArgumentException>(() => repository.Create("Invalid", new[] { "" }));
            Assert.That(File.ReadAllText(savePath), Is.EqualTo(before));
            Assert.That(repository.Sets[0].Name, Is.EqualTo("Food"));
        }

        [Test]
        public void FailedDiskCommitDoesNotChangeMemoryOrLeaveTemporaryFiles()
        {
            var repository = new CustomSetRepository(savePath);
            repository.Load();
            // A directory occupying the target file path forces a portable write failure.
            Directory.CreateDirectory(savePath);
            Assert.Catch<IOException>(() => repository.Create("Food", new[] { "apple" }));
            Assert.That(repository.Sets, Is.Empty);
            Assert.That(Directory.GetFiles(testDirectory, "*.tmp-*"), Is.Empty);
        }

        [Test]
        public void CorruptPrimaryRecoversPreviousSaveAndPreservesDamagedFileOnNextSave()
        {
            var repository = new CustomSetRepository(savePath);
            VocabularySet saved = repository.Create("First", new[] { "apple" });
            repository.Create("Second", new[] { "pear" });
            File.WriteAllText(savePath, "broken-primary");
            var recovered = new CustomSetRepository(savePath);
            Assert.That(recovered.Load().Count, Is.EqualTo(1));
            Assert.That(recovered.Sets[0].Id, Is.EqualTo(saved.Id));
            Assert.That(recovered.RecoveryNotice, Is.Not.Null.And.Not.Empty);
            Assert.That(File.ReadAllText(savePath), Is.EqualTo("broken-primary"));
            recovered.Create("Recovered edit", new[] { "peach" });
            string[] archives = Directory.GetFiles(testDirectory, "*.corrupt-*");
            Assert.That(archives.Length, Is.EqualTo(1));
            Assert.That(File.ReadAllText(archives[0]), Is.EqualTo("broken-primary"));
            Assert.That(new CustomSetRepository(savePath).Load().Count, Is.EqualTo(2));
            Assert.That(File.ReadAllText(savePath + ".bak"), Does.Contain(saved.Id));
        }

        [Test]
        public void MissingPrimaryCanRecoverBackup()
        {
            var repository = new CustomSetRepository(savePath);
            VocabularySet first = repository.Create("First", new[] { "apple" });
            repository.Save();
            File.Delete(savePath);
            var recovered = new CustomSetRepository(savePath);
            Assert.That(recovered.Load()[0].Id, Is.EqualTo(first.Id));
            recovered.Save();
            Assert.That(new CustomSetRepository(savePath).Load()[0].Id, Is.EqualTo(first.Id));
        }

        [TestCase("{}")]
        [TestCase("not-json")]
        [TestCase("")]
        public void UnrecoverableSaveIsNeverSilentlyOverwritten(string invalid)
        {
            File.WriteAllText(savePath, invalid);
            var repository = new CustomSetRepository(savePath);
            Assert.Throws<InvalidDataException>(() => repository.Load());
            Assert.Throws<InvalidDataException>(() => repository.Create("New", new[] { "apple" }));
            Assert.That(File.ReadAllText(savePath), Is.EqualTo(invalid));
        }

        [Test]
        public void CorruptPrimaryAndBackupAreBothPreserved()
        {
            File.WriteAllText(savePath, "bad-primary");
            File.WriteAllText(savePath + ".bak", "bad-backup");
            var repository = new CustomSetRepository(savePath);
            Assert.Throws<InvalidDataException>(() => repository.Save());
            Assert.That(File.ReadAllText(savePath), Is.EqualTo("bad-primary"));
            Assert.That(File.ReadAllText(savePath + ".bak"), Is.EqualTo("bad-backup"));
        }

        [Test]
        public void FutureSaveVersionCannotBeReplacedByAnOlderBackup()
        {
            var repository = new CustomSetRepository(savePath);
            repository.Create("Food", new[] { "apple" });
            repository.Save();
            string future = File.ReadAllText(savePath).Replace("\"version\": 1", "\"version\": 999");
            File.WriteAllText(savePath, future);
            var futureReader = new CustomSetRepository(savePath);
            Assert.Throws<NotSupportedException>(() => futureReader.Load());
            Assert.Throws<NotSupportedException>(() => futureReader.Create("Replacement", new[] { "pear" }));
            Assert.That(File.ReadAllText(savePath), Is.EqualTo(future));
        }

        [Test]
        public void AFailedReloadBlocksWritesFromPreviouslyLoadedRepository()
        {
            var repository = new CustomSetRepository(savePath);
            repository.Create("Food", new[] { "apple" });
            File.WriteAllText(savePath, "new-corruption");
            Assert.Throws<InvalidDataException>(() => repository.Load());
            Assert.Throws<InvalidDataException>(() => repository.Save());
            Assert.That(File.ReadAllText(savePath), Is.EqualTo("new-corruption"));
        }

        [Test]
        public void ChangesOutsideTheRepositoryCannotBeSilentlyOverwritten()
        {
            var repository = new CustomSetRepository(savePath);
            repository.Create("Food", new[] { "apple" });
            var otherWriter = new CustomSetRepository(savePath);
            otherWriter.Create("Tools", new[] { "hammer" });
            string externalSave = File.ReadAllText(savePath);
            Assert.Throws<IOException>(() => repository.Create("Stale edit", new[] { "pear" }));
            Assert.That(File.ReadAllText(savePath), Is.EqualTo(externalSave));
            Assert.That(repository.Load().Count, Is.EqualTo(2));
        }

        private static void CompleteRound(TestSession session, IEnumerable<string> misses)
        {
            var missed = new HashSet<string>(misses);
            while (!session.IsRoundComplete)
            {
                TestQuestion question = session.CurrentQuestion;
                string answer = missed.Contains(question.TargetId)
                    ? question.ChoiceIds.First(id => id != question.TargetId)
                    : question.TargetId;
                session.SubmitAnswer(question.Sequence, answer);
            }
        }
    }
}
