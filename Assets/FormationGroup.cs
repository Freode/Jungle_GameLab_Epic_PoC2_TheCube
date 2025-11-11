using System.Collections.Generic;
using UnityEngine;

public enum FormationMode
{
    Individual, // 0: Cubes fall individually
    Cluster,    // 1: Fall only when all cubes in the group are ungrounded
    Solid,      // 2: Fall only when all 1st-floor cubes are ungrounded
    Ladder      // 3: Fall only when the bottom cube is ungrounded
}

public class FormationGroup : MonoBehaviour
{
    public List<Unit> units = new List<Unit>();
    public FormationMode currentMode = FormationMode.Individual;

    // Helper method to check if all units in the group are ungrounded
    private bool AreAllUnitsUngrounded()
    {
        if (units.Count == 0) return false;

        foreach (var unit in units)
        {
            if (unit.IsGrounded)
            {
                return false; // Found a grounded unit, so not all are ungrounded
            }
        }
        return true; // No grounded units were found
    }

    void Update()
    {
        if (units.Count == 0) return;

        bool shouldFall = false;

        switch (currentMode)
        {
            case FormationMode.Cluster:
                // Fall if all units in the group are ungrounded.
                if (AreAllUnitsUngrounded())
                {
                    shouldFall = true;
                }
                break;

            case FormationMode.Solid:
                // Fall if all "first floor" units are ungrounded.
                // We identify first-floor units by their relative y-position.
                // Assuming the lowest units are on the first floor.
                float minY = float.MaxValue;
                foreach (var unit in units)
                {
                    if (unit.transform.localPosition.y < minY)
                    {
                        minY = unit.transform.localPosition.y;
                    }
                }

                bool allFirstFloorUngrounded = true;
                foreach (var unit in units)
                {
                    // Check units that are on the lowest level (with a small tolerance)
                    if (Mathf.Abs(unit.transform.localPosition.y - minY) < 0.1f)
                    {
                        if (unit.IsGrounded)
                        {
                            allFirstFloorUngrounded = false;
                            break;
                        }
                    }
                }

                if (allFirstFloorUngrounded && units.Count > 0)
                {
                    shouldFall = true;
                }
                break;

            case FormationMode.Ladder:
                // Fall if the single bottom-most unit is ungrounded.
                Unit bottomUnit = null;
                float lowestY = float.MaxValue;
                foreach (var unit in units)
                {
                    if (unit.transform.position.y < lowestY)
                    {
                        lowestY = unit.transform.position.y;
                        bottomUnit = unit;
                    }
                }

                if (bottomUnit != null && !bottomUnit.IsGrounded)
                {
                    shouldFall = true;
                }
                break;
        }

        if (shouldFall)
        {
            foreach (var unit in units)
            {
                unit.Fall();
            }
        }
    }
}
