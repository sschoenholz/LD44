using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VisualizationModeButton : MonoBehaviour
{
    public GameObject button;
    public Color color;
    public VisualizationMode mode;

    public Camera camera;
    public Map map;

    private bool is_down;

    // Start is called before the first frame update
    void Start()
    {
        GetComponent<Renderer>().material.SetColor("_Color", color);
    }

    // Update is called once per frame
    void Update()
    {
        var ones = new Vector3(1.0f, 1.0f, 1.0f);
        if (Input.GetButtonDown("Select"))
        {
            RaycastHit hit;
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit))
            {
                if (button.transform == hit.transform)
                {
                    map.SetVisualizationMode(mode);
                    GetComponent<Transform>().localScale = 1.0f * ones;
                }
            }
        }
        if (Input.GetButtonUp("Select"))
        {
            GetComponent<Transform>().localScale = 1.2f * ones;
        }

    }
}
