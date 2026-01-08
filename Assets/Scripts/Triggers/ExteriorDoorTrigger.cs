using UnityEngine;

public class ExteriorDoorTrigger : MonoBehaviour
{
    void Start()
    {
        GetComponent<Renderer>().enabled = false;
    }

    void OnTriggerEnter()
    {
        Debug.Log(Random.Range(0, 100));
    }
}
