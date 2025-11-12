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
    public bool isTransitioning = false;

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

    // Helper method to check if any unit in the group is ungrounded
    private bool IsAnyUnitUngrounded()
    {
        if (units.Count == 0) return false;
        foreach (var unit in units)
        {
            if (!unit.IsGrounded)
            {
                return true; // Found an ungrounded unit
            }
        }
        return false; // All units are grounded
    }

    void Update()
    {
        if (isTransitioning) return;
        if (units.Count == 0) return;

        bool shouldFall = false;

        switch (currentMode)
        {
            case FormationMode.Cluster:
                List<Unit> ungroundedUnits = new List<Unit>();
                List<Unit> groundedUnits = new List<Unit>();

                foreach (var unit in units)
                {
                    if (!unit.IsGrounded)
                    {
                        ungroundedUnits.Add(unit);
                    }
                    else
                    {
                        groundedUnits.Add(unit);
                    }
                }

                if (AreAllUnitsUngrounded())
                {
                    // 1. Let ungrounded units continue to fall (they are already in Individual mode or will be set to it)
                    foreach (var unit in ungroundedUnits)
                    {
                        unit.currentMode = FormationMode.Individual;
                        // Explicitly deselect the falling unit
                        if (PlayerController.instance != null)
                        {
                            PlayerController.instance.DeselectUnit(unit);
                        }
                    }

                    // 2. For grounded units, switch them to Individual mode without animation
                    if (PlayerController.instance != null)
                    {
                        PlayerController.instance.DisbandFormationWithoutAnimation(groundedUnits);
                    }

                    // Clear the formationGroup's unit list as the cluster is broken
                    units.Clear();
                    // Also, set the currentMode of FormationGroup to Individual
                    currentMode = FormationMode.Individual;
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
                    // 사다리 모드에서 바닥 유닛이 ungrounded 상태가 되면,
                    // 사다리 포메이션을 해체하고 유닛들을 개별 모드로 전환하여 각자 낙하하도록 처리
                    List<Unit> unitsToDisband = new List<Unit>(units); // 현재 포메이션의 모든 유닛 복사
                    units.Clear(); // FormationGroup의 units 리스트 비우기
                    currentMode = FormationMode.Individual; // FormationGroup 모드를 Individual로 변경

                    if (PlayerController.instance != null)
                    {
                        // PlayerController를 통해 포메이션 해체 및 유닛들 개별 모드로 전환
                        PlayerController.instance.DisbandFormationWithoutAnimation(unitsToDisband);
                    }
                    // 이 경우, 개별 유닛들은 각자의 Update()에서 IsGrounded 상태에 따라 Fall()을 호출하게 됨.
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
