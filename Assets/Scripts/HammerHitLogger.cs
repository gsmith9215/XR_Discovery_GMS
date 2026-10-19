
using UnityEngine;

public class HammerHitLogger : MonoBehaviour
{
    [SerializeField] private RandomObjectSpawner spawner;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Hammer hit: " + other.gameObject.name);

        if (spawner == null)
        {
            Debug.LogError("Spawner is NOT assigned!");
            return;
        }

        Debug.Log("Sending hit to spawner.");
        spawner.SelectBox(other);
    }
}
