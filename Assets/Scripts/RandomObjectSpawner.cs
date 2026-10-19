
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using TMPro;

public class RandomObjectSpawner : MonoBehaviour
{
    [Header("VR References")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform rightHandAnchor;

    // Vocabulary Prefabs are loaded automatically.
    private GameObject[] objectPrefabs;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnDistance = 1.5f;
    [SerializeField] private float objectSpacing = 0.6f;
    [SerializeField] private float heightBelowEyes = 0.1f;

    private const int ItemsPerRound = 3;

    private readonly List<GameObject> activeObjects = new();
    private readonly List<int> remainingIndices = new();

    private readonly Vector3[] spawnPositions =
        new Vector3[ItemsPerRound];

    private int roundNumber = 0;

    [Header("Scoreboard")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text targetText;

    private int score = 0;
    private GameObject targetObject;

    private void Start()
    {
        // Automatically load completed vocabulary boxes.
        objectPrefabs = Resources.LoadAll<GameObject>(
            "VocabularyBoxes"
        );

        Debug.Log(
            $"Loaded {objectPrefabs.Length} vocabulary objects."
        );

        if (playerCamera == null)
        {
            Debug.LogError("Player Camera is not assigned!");
            enabled = false;
            return;
        }

        if (objectPrefabs.Length < 3)
        {
            Debug.LogError(
                "At least 3 vocabulary Prefabs are required!"
            );
            enabled = false;
            return;
        }

        if (scoreText != null)
            scoreText.text = "Score: 0";

        CalculateSpawnPositions();
        ShuffleObjects();
        SpawnNextGroup();
    }

    private void Update()
    {
    }

    private void CalculateSpawnPositions()
    {
        Vector3 forward = Vector3.ProjectOnPlane(
            playerCamera.forward,
            Vector3.up
        ).normalized;

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        Vector3 right = Vector3.Cross(
            Vector3.up,
            forward
        );

        Vector3 center = playerCamera.position
            + forward * spawnDistance
            - Vector3.up * heightBelowEyes;

        for (int i = 0; i < ItemsPerRound; i++)
        {
            spawnPositions[i] = center
                + right * ((i - 1) * objectSpacing);
        }
    }

    private void ShuffleObjects()
    {
        remainingIndices.Clear();

        for (int i = 0; i < objectPrefabs.Length; i++)
        {
            if (objectPrefabs[i] != null)
                remainingIndices.Add(i);
        }

        if (remainingIndices.Count < ItemsPerRound)
        {
            Debug.LogError("Not enough valid Prefabs!");
            enabled = false;
            return;
        }

        // Fisher-Yates shuffle.
        for (int i = remainingIndices.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);

            int temp = remainingIndices[i];
            remainingIndices[i] = remainingIndices[j];
            remainingIndices[j] = temp;
        }

        roundNumber = 0;
        Debug.Log("Vocabulary objects shuffled!");
    }

    public void SpawnNextGroup()
    {
        if (!enabled)
            return;

        ClearObjects();

        if (remainingIndices.Count < ItemsPerRound)
        {
            ShuffleObjects();
        }

        roundNumber++;

        for (int i = 0; i < ItemsPerRound; i++)
        {
            int prefabIndex = remainingIndices[0];
            remainingIndices.RemoveAt(0);

            // Spawn the complete vocabulary box Prefab.
            GameObject newObject = Instantiate(
                objectPrefabs[prefabIndex],
                spawnPositions[i],
                Quaternion.identity
            );

            activeObjects.Add(newObject);
        }

        // Randomly choose one of the three as the correct answer.
        targetObject = activeObjects[
            UnityEngine.Random.Range(0, activeObjects.Count)
        ];

        // Display its English name.
        if (targetText != null)
        {
            string word = targetObject.name.Replace("(Clone)", "").Trim();
            targetText.text = "Find: " + word;
        }

        Debug.Log("Vocabulary Round: " + roundNumber);
    }

    private void ClearObjects()
    {
        foreach (GameObject obj in activeObjects)
        {
            if (obj != null)
                Destroy(obj);
        }

        activeObjects.Clear();
    }

    
    private bool processingHit = false;

    public void SelectBox(Collider hit)
    {
        if (processingHit) return;

        foreach (GameObject box in activeObjects)
        {
            if (box != null &&
                (hit.transform == box.transform ||
                hit.transform.IsChildOf(box.transform)))
            {
                if (box == targetObject)
                {
                    score++;

                    if (scoreText != null)
                        scoreText.text = "Score: " + score;

                    Debug.Log("Correct! Score: " + score);
                }
                else
                {
                    Debug.Log("Incorrect: " + box.name);
                }

                StartCoroutine(AdvanceRound());
                return;
            }
        }
    }

    private IEnumerator AdvanceRound()
    {
        processingHit = true;

        // Immediately spawn the next group.
        SpawnNextGroup();

        // Brief cooldown to prevent accidental double hits.
        yield return new WaitForSeconds(0.3f);

        processingHit = false;
    }
}
