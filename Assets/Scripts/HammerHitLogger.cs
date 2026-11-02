
using UnityEngine;

public class HammerHitLogger : MonoBehaviour
{
    [SerializeField] private RandomObjectSpawner spawner;

    private void OnTriggerEnter(Collider other)
    {
        if (spawner == null)
        {
            return;
        }

        spawner.SelectBox(other);
    }
}
