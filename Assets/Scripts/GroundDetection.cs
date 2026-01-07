

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
        if (collision.gameObject.tag != "Ground")
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
        // Vector3 normal = collision.gameObject.transform.up;

        // get average normals of contact points
        Vector3 normal = Vector3.zero;
        foreach (ContactPoint contact in collision.contacts)
        {
            normal += contact.normal;
        }
        normal /= collision.contacts.Length;
        
        contactNormals.Add(collision.gameObject, normal);
        playerControl.groundAvgNormal = CalculateGroundAvgNormal();
        playerControl.isGrounded = true;
    }

    private float lastGroundCheckTime = 0.0f;

    void OnCollisionStay(Collision collision)
    {
        // watch for new contact points and update the normal if they change
        if (collision.gameObject.tag != "Ground")
        {
            return;
        }

        // only trigger every 0.1 seconds to avoid spamming the console
        if (Time.time - lastGroundCheckTime < 0.1f)
        {
            return;
        }

        Vector3 normal = Vector3.zero;
        foreach (ContactPoint contact in collision.contacts)
        {
            normal += contact.normal;
        }
        normal /= collision.contacts.Length;

        if (contactNormals.ContainsKey(collision.gameObject))
        {
            contactNormals[collision.gameObject] = normal;
        }
        else
        {
            contactNormals.Add(collision.gameObject, normal);
        }
        playerControl.groundAvgNormal = CalculateGroundAvgNormal();
        playerControl.isGrounded = true;
        lastGroundCheckTime = Time.time;
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.tag != "Ground")
        {
            return;
        }
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
