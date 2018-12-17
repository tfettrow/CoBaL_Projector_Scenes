using UnityEngine;
using System.Collections;

public class TargetArea : MonoBehaviour
{
    public GameObject cogCursor;
    public static float counter = 0;
    public static float TimeSinceReset = 0;
    private SpriteRenderer sRenderer;

    private Color normalColor, activeColor;
	// Use this for initialization
	void Start ()
	{
	    sRenderer = GetComponent<SpriteRenderer>();
	    normalColor = sRenderer.color;
	    normalColor.a = .5f;
        activeColor = Color.green;
	    activeColor.a = .5f;
	    sRenderer.color = normalColor;
	}
	
	// Update is called once per frame
	void Update ()
	{
	    TimeSinceReset += Time.deltaTime;
	}

    void OnTriggerStay(Collider other)
    {
        if (other.gameObject == cogCursor)
        {
            sRenderer.color = activeColor;
            counter += Time.deltaTime;
        }
    }

    void OnTriggerExit(Collider other)
    {
        sRenderer.color = normalColor;
    }

    public static void Reset()
    {
        counter = 0;
        TimeSinceReset = 0;
    }
}
