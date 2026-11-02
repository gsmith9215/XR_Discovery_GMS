using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace XRDiscovery.Vocabulary
{
    public sealed class TestQuestion
    {
        public int Sequence { get; }
        public string TargetId { get; }
        public IReadOnlyList<string> ChoiceIds { get; }

        internal TestQuestion(int sequence, string targetId, List<string> choiceIds)
        {
            Sequence = sequence;
            TargetId = targetId;
            ChoiceIds = new ReadOnlyCollection<string>(choiceIds);
        }
    }

    /// <summary>
    /// Owns a finite test round. Seeing a distractor does not count as answering that word.
    /// The presentation layer submits the question sequence so a second hit cannot answer
    /// the next question accidentally.
    /// </summary>
    public sealed class TestSession
    {
        private readonly List<string> originalIds;
        private readonly List<string> availableIds;
        private readonly Random random;
        private readonly Dictionary<string, string> answerKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> missedIds = new List<string>();
        private List<string> roundIds;
        private int sequence;

        public TestQuestion CurrentQuestion { get; private set; }
        public int RoundNumber { get; private set; }
        public int AnsweredCount { get; private set; }
        public int CorrectCount { get; private set; }
        public bool IsRoundComplete => CurrentQuestion == null;
        public IReadOnlyList<string> OriginalItemIds => originalIds.AsReadOnly();
        public IReadOnlyList<string> RoundItemIds => roundIds.AsReadOnly();
        public IReadOnlyList<string> MissedItemIds => missedIds.AsReadOnly();

        public TestSession(IEnumerable<string> selectedIds, IEnumerable<string> availableIds,
            Random random = null, Func<string, string> answerKey = null)
        {
            originalIds = VocabularySet.CopyDistinctIds(selectedIds);
            if (originalIds.Count == 0)
                throw new ArgumentException("Choose a set with at least one vocabulary item.", nameof(selectedIds));

            this.availableIds = VocabularySet.CopyDistinctIds(availableIds);
            foreach (string id in this.availableIds)
            {
                string key = answerKey == null ? id : answerKey(id);
                answerKeys.Add(id, string.IsNullOrWhiteSpace(key) ? id : key.Trim().Normalize());
            }
            var available = new HashSet<string>(this.availableIds, StringComparer.Ordinal);
            foreach (string id in originalIds)
            {
                if (!available.Contains(id))
                    throw new ArgumentException("A selected vocabulary item is unavailable: " + id,
                        nameof(selectedIds));
            }

            this.random = random ?? new Random();
            RoundNumber = 1;
            StartRound(originalIds);
        }

        public bool SubmitAnswer(int questionSequence, string choiceId)
        {
            if (CurrentQuestion == null)
                throw new InvalidOperationException("This round is already complete.");
            if (questionSequence != CurrentQuestion.Sequence)
                throw new InvalidOperationException("That answer belongs to a previous question.");
            if (!Contains(CurrentQuestion.ChoiceIds, choiceId))
                throw new ArgumentException("Choose one of the displayed vocabulary items.", nameof(choiceId));

            bool correct = string.Equals(choiceId, CurrentQuestion.TargetId, StringComparison.Ordinal);
            if (correct)
                CorrectCount++;
            else
                missedIds.Add(CurrentQuestion.TargetId);

            AnsweredCount++;
            CurrentQuestion = AnsweredCount < roundIds.Count ? MakeQuestion(roundIds[AnsweredCount]) : null;
            return correct;
        }

        public bool StartNextMissedRound()
        {
            if (!IsRoundComplete)
                throw new InvalidOperationException("Finish this round before starting the next one.");
            if (missedIds.Count == 0)
                return false;

            var nextTargets = new List<string>(missedIds);
            RoundNumber++;
            StartRound(nextTargets);
            return true;
        }

        public void Retest()
        {
            RoundNumber = 1;
            StartRound(originalIds);
        }

        private void StartRound(IEnumerable<string> targets)
        {
            roundIds = new List<string>(targets);
            Shuffle(roundIds);
            missedIds.Clear();
            AnsweredCount = 0;
            CorrectCount = 0;
            CurrentQuestion = MakeQuestion(roundIds[0]);
        }

        private TestQuestion MakeQuestion(string targetId)
        {
            var choices = new List<string> { targetId };
            var choiceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { answerKeys[targetId] };
            var selectedDistractors = new List<string>();
            foreach (string id in originalIds)
                if (!string.Equals(id, targetId, StringComparison.Ordinal))
                    selectedDistractors.Add(id);
            Shuffle(selectedDistractors);
            AddChoices(choices, selectedDistractors, choiceKeys);

            if (choices.Count < 3)
            {
                var otherDistractors = new List<string>();
                foreach (string id in availableIds)
                    if (!choices.Contains(id))
                        otherDistractors.Add(id);
                Shuffle(otherDistractors);
                AddChoices(choices, otherDistractors, choiceKeys);
            }

            Shuffle(choices);
            return new TestQuestion(++sequence, targetId, choices);
        }

        private void AddChoices(List<string> choices, List<string> candidates, HashSet<string> choiceKeys)
        {
            foreach (string id in candidates)
            {
                if (choices.Count == 3)
                    break;
                if (choiceKeys.Add(answerKeys[id]))
                    choices.Add(id);
            }
        }

        private void Shuffle(List<string> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                string value = values[i];
                values[i] = values[j];
                values[j] = value;
            }
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }
}
