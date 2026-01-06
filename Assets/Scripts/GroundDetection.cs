

using System.Collections.Generic;
using UnityEngine;

public class GroundDetection : MonoBehaviour
{

    public SpacePlayerControl playerControl;

    private Dictionary<GameObject, Vector3> contactNormals = new Dictionary<GameObject, Vector3>();

    // Collision

    private Vector3 CalculateGroundAvgNormal()
    {
        Vector3 avgContactNormal = Vector3.zero;
        foreach (Vector3 normal in contactNormals.Values)
        {
            avgContactNormal += normal;
        }
        avgContactNormal /= contactNormals.Count;
        return avgContactNormal;
    }


    void OnCollisionEnter(Collision collision)
    {
        // Ignore the parent object (the player)
        if (collision.gameObject == playerControl.gameObject)
        {
            return;
        }

        Debug.Log($"Collided with: {collision.gameObject.name}");
        // Access contact points: Collision.GetContact(0).point

        // remove the contact normal for this collision if it already exists
        if (contactNormals.ContainsKey(collision.gameObject))
        {
            contactNormals.Remove(collision.gameObject);
        }

        // get up of the entire object
        Vector3 normal = collision.gameObject.transform.up;
        contactNormals.Add(collision.gameObject, normal);
        playerControl.groundAvgNormal = CalculateGroundAvgNormal();
        playerControl.isGrounded = true;
    }

    void OnCollisionStay(Collision collision)
    {
        // Do something every frame the collision is active
    }

    void OnCollisionExit(Collision collision)
    {
        Debug.Log($"Stopped colliding with: {collision.gameObject.name}");
        // Do something when the collision ends
        contactNormals.Remove(collision.gameObject);

        if (contactNormals.Count > 0)
        {
            playerControl.groundAvgNormal = CalculateGroundAvgNormal();
        } else
        {
            playerControl.isGrounded = false;
        }    
    }
}
