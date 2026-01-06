using UnityEngine;

public class GroundConsoleBlink : MonoBehaviour
{
    public bool IncomingTransmission;

    [Header("Blink Pattern")]
    public float OnTime;
    public int OnNum;
    public float OffTime;

    private float lastTime = -Mathf.Infinity;
    private float lastMorse = -Mathf.Infinity;
    private float morseSeperation;
    private bool on = false;
    private bool morseoff = false;

    private bool answering = false;

    // Update is called once per frame
    void Update()
    {
        TransmissionInteract parent = GetComponentInParent<TransmissionInteract>();
        if (parent == null)
        {
            Debug.LogError("ParentScript not found on parent!");
        }
        else
        {
            answering = parent.answeringTransmission;
        }

        if (IncomingTransmission && !answering)
        {
            float timeSince = Time.time - lastTime;
            if (timeSince > (on ? OnTime : OffTime)) 
            {
                morseSeperation = OnNum > 1 ? OnTime / (OnNum * 2 - 1) : OnTime;
                SetEmission(GetComponent<Renderer>(), true, on ? Color.black : Color.red);
                on = !on;
                // if (on) Debug.Log("On");
                // else Debug.Log("Off");
                lastTime = Time.time;
                morseoff = false;
                lastMorse = -Mathf.Infinity;
            }
            else if (on)
            {
                float timeSinceMorse = Time.time - lastMorse;
                if (timeSinceMorse > morseSeperation) {
                    // Debug.Log(timeSinceMorse);
                    SetEmission(GetComponent<Renderer>(), true, morseoff ? Color.black : Color.red);
                    morseoff = !morseoff;
                    lastMorse = Time.time;
                }
            }
        } 
        else if (on)
        {
            SetEmission(GetComponent<Renderer>(), true, Color.black);
            on = false;
            lastTime = -Mathf.Infinity;
        }
    }

    void SetEmission(Renderer renderer, bool enabled, Color color)
    {
        Material mat = renderer.material;

        if (enabled)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color);
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }
    }
}
