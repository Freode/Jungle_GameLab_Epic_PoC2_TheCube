using System.Collections.Generic;
using UnityEngine;

public class LineDrawer : MonoBehaviour
{
    public GameObject linePrefab;
    private List<LineRenderer> lineRenderers = new List<LineRenderer>();
    private List<Unit> trackedUnits = new List<Unit>();

    // Call this to set up the lines between units
    public void DrawLines(List<Unit> units)
    {
        ClearLines();

        if (units == null || units.Count < 2)
        {
            return;
        }

        trackedUnits = new List<Unit>(units);

        for (int i = 0; i < trackedUnits.Count; i++)
        {
            GameObject lineObj = Instantiate(linePrefab, transform);
            LineRenderer lr = lineObj.GetComponent<LineRenderer>();
            lr.positionCount = 2;
            lineRenderers.Add(lr);
        }
    }

    // Clear all lines and stop tracking units
    public void ClearLines()
    {
        foreach (var lr in lineRenderers)
        {
            Destroy(lr.gameObject);
        }
        lineRenderers.Clear();
        trackedUnits.Clear();
    }

    // Update is called once per frame
    void Update()
    {
        if (trackedUnits.Count < 2)
        {
            return;
        }

        for (int i = 0; i < trackedUnits.Count; i++)
        {
            LineRenderer lr = lineRenderers[i];
            Unit startUnit = trackedUnits[i];
            Unit endUnit = trackedUnits[(i + 1) % trackedUnits.Count]; // Loop back to the first unit

            lr.SetPosition(0, startUnit.transform.position);
            lr.SetPosition(1, endUnit.transform.position);
        }
    }
}
