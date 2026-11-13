
using UnityEngine;

public class HammerHitLogger : MonoBehaviour
{
    [SerializeField] private VocabularyObjectPresenter spawner;

    private void OnTriggerEnter(Collider other)
    {
        if (spawner == null)
        {
            return;
        }

        spawner.SelectBox(other);
    }
}
