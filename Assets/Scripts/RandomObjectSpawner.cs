using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using XRDiscovery.Vocabulary;

// Keeps the scene's camera, spacing and hammer references; VocabularyApp owns session rules.
public class RandomObjectSpawner : MonoBehaviour
{
    [Header("VR References")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform rightHandAnchor;
    [Header("Spawn Settings")]
    [SerializeField] private float spawnDistance = 1f;
    [SerializeField] private float objectSpacing = 0.5f;
    [SerializeField] private float heightBelowEyes = 0.25f;
    [Header("Study Display")]
    [SerializeField] private float studyDistance = 1.5f;
    [SerializeField] private float studyHeightBelowEyes = 0.1f;
    [Header("Legacy Scoreboard")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text targetText;

    private readonly List<GameObject> activeObjects = new();
    private readonly List<string> activeIds = new();
    private Transform presentationRoot;
    private bool acceptingHits;
    private float acceptAfter;
    private bool positioned;
    private bool presentationIsTest;
    private Vector3 displayPosition;
    private Quaternion displayRotation;
    public event Action<string> BoxSelected;
    public Transform PlayerCamera => playerCamera;
    public Transform RightHandAnchor => rightHandAnchor;
    public IReadOnlyList<GameObject> ActiveObjects => activeObjects;

    private void Start()
    {
        if (playerCamera == null && Camera.main != null) playerCamera = Camera.main.transform;
        if (playerCamera == null)
        {
            Debug.LogError("Vocabulary requires a player camera.", this);
            enabled = false;
            return;
        }
        if (scoreText != null) scoreText.gameObject.SetActive(false);
        if (targetText != null) targetText.gameObject.SetActive(false);
        var app = GetComponent<VocabularyApp>();
        if (app == null) app = gameObject.AddComponent<VocabularyApp>();
        app.Initialize(this);
    }

    public void ShowItems(IReadOnlyList<VocabularyEntry> entries, bool allowAnswers)
    {
        ClearObjects();
        if (playerCamera == null) throw new InvalidOperationException("Player camera is not assigned.");
        bool modeChanged = presentationIsTest != allowAnswers;
        presentationIsTest = allowAnswers;
        if (!positioned || modeChanged) Recenter();
        presentationRoot = new GameObject("Vocabulary display").transform;
        presentationRoot.SetParent(transform, false);
        presentationRoot.SetPositionAndRotation(displayPosition, displayRotation);
        for (int i = 0; i < entries.Count; i++)
        {
            var prefab = entries[i].Prefab;
            if (prefab == null) throw new InvalidOperationException("Missing vocabulary box: " + entries[i].Name);
            var box = Instantiate(prefab, presentationRoot);
            box.name = entries[i].Name;
            box.transform.localPosition = Vector3.right * ((i - (entries.Count - 1) * .5f) * objectSpacing);
            box.transform.localRotation = Quaternion.identity;
            activeObjects.Add(box);
            activeIds.Add(entries[i].Id);
        }
        acceptingHits = allowAnswers;
        acceptAfter = Time.unscaledTime + .35f;
    }

    public void SelectBox(Collider hit)
    {
        if (!acceptingHits || hit == null || Time.unscaledTime < acceptAfter) return;
        for (int i = 0; i < activeObjects.Count; i++)
        {
            var box = activeObjects[i];
            if (box == null || !(hit.transform == box.transform || hit.transform.IsChildOf(box.transform))) continue;
            acceptingHits = false;
            BoxSelected?.Invoke(activeIds[i]);
            return;
        }
    }

    public void StopAcceptingHits() => acceptingHits = false;

    public void Recenter()
    {
        if (playerCamera == null) return;
        var forward = Vector3.ProjectOnPlane(playerCamera.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
        displayPosition = playerCamera.position + forward * (presentationIsTest ? spawnDistance : studyDistance)
            - Vector3.up * (presentationIsTest ? heightBelowEyes : studyHeightBelowEyes);
        displayRotation = Quaternion.LookRotation(forward, Vector3.up);
        positioned = true;
        if (presentationRoot != null) presentationRoot.SetPositionAndRotation(displayPosition, displayRotation);
    }

    public void ClearObjects()
    {
        acceptingHits = false;
        if (presentationRoot != null)
        {
            presentationRoot.gameObject.SetActive(false);
            Destroy(presentationRoot.gameObject);
            presentationRoot = null;
        }
        activeObjects.Clear();
        activeIds.Clear();
    }

    private void OnDestroy() => ClearObjects();
}
