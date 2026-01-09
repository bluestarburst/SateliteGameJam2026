


using System.Collections.Generic;
using UnityEngine;

public class groundCount : MonoBehaviour
{

    public SpacePlayerControl playerControl;

    void OnCollisionEnter(Collision collision)
    {
        // Ignore the parent object (the player)
        if (collision.gameObject.tag != "Ground")
        {
            return;
        }

        Debug.Log($"Collided with: {collision.gameObject.name}");
        playerControl.groundCount++;
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.tag != "Ground")
        {
            return;
        }

        Debug.Log($"Exited collision with: {collision.gameObject.name}");
        playerControl.groundCount--;
    }
}
