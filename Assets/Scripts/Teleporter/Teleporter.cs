using UnityEngine;

public class Teleporter : MonoBehaviour
{
    public GameObject destination;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            other.transform.position = destination.transform.position;
        }
    }
}
