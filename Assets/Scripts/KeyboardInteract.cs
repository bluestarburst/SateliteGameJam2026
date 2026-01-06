using UnityEngine;

public class KeyboardInteract : MonoBehaviour, IInteractable
{
    public void Interact() {
        Debug.Log(Random.Range(0, 100));
    }
}
