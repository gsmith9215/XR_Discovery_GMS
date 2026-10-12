

using System.Collections.Generic;
using UnityEngine;

public class RandomObjectSpawner : MonoBehaviour
{
    [Header("VR References")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform rightHandAnchor;

    [Header("Vocabulary Objects")]
    [SerializeField] private GameObject[] objectPrefabs;

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

    private void Start()
    {
        if (playerCamera == null)
        {
            Debug.LogError("Player Camera is not assigned!");
            enabled = false;
            return;
        }

        if (objectPrefabs == null || objectPrefabs.Length < 3)
        {
            Debug.LogError("At least 3 prefabs are required!");
            enabled = false;
            return;
        }

        CalculateSpawnPositions();
        ShuffleObjects();
        SpawnNextGroup();
    }

    private void Update()
    {
        // A button on right controller.
        bool buttonA = OVRInput.GetDown(
            OVRInput.Button.One,
            OVRInput.Controller.RTouch
        );

        // X button on left controller.
        bool buttonX = OVRInput.GetDown(
            OVRInput.Button.One,
            OVRInput.Controller.LTouch
        );

        // Right index trigger.
        bool rightTrigger = OVRInput.GetDown(
            OVRInput.Button.PrimaryIndexTrigger,
            OVRInput.Controller.RTouch
        );

        // Left index trigger.
        bool leftTrigger = OVRInput.GetDown(
            OVRInput.Button.PrimaryIndexTrigger,
            OVRInput.Controller.LTouch
        );

        // Advance when any supported button is pressed.
        if (buttonA || buttonX || rightTrigger || leftTrigger)
        {
            Debug.Log("Controller input detected!");
            SpawnNextGroup();
        }
    }

    private void CalculateSpawnPositions()
    {
        Vector3 forward = Vector3.ProjectOnPlane(
            playerCamera.forward,
            Vector3.up
        ).normalized;

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

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

        // Collect valid prefab indices.
        for (int i = 0; i < objectPrefabs.Length; i++)
        {
            if (objectPrefabs[i] != null)
            {
                remainingIndices.Add(i);
            }
        }

        if (remainingIndices.Count < ItemsPerRound)
        {
            Debug.LogError("Not enough valid prefabs!");
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

        // Remove the current three objects.
        ClearObjects();

        // Restart when fewer than three objects remain.
        if (remainingIndices.Count < ItemsPerRound)
        {
            Debug.Log("Vocabulary cycle completed!");
            ShuffleObjects();
        }

        roundNumber++;

        // Spawn three unique objects.
        for (int i = 0; i < ItemsPerRound; i++)
        {
            int prefabIndex = remainingIndices[0];
            remainingIndices.RemoveAt(0);

            GameObject newObject = Instantiate(
                objectPrefabs[prefabIndex],
                spawnPositions[i],
                Quaternion.identity
            );

            // Align the visible center of the object.
            CenterObject(newObject, spawnPositions[i]);

            activeObjects.Add(newObject);
        }

        Debug.Log("Vocabulary Round: " + roundNumber);
    }

    private void CenterObject(GameObject obj, Vector3 target)
    {
        Renderer[] renderers =
            obj.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;

        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }

        Vector3 offset = target - bounds.center;

        obj.transform.position += offset;
    }

    private void ClearObjects()
    {
        foreach (GameObject obj in activeObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }

        activeObjects.Clear();
    }
}
