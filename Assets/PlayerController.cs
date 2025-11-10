using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum FormationMode { None, Mode2D, Mode3D }

public class FormationSlot
{
    public Unit unit;
    public Vector3 offset; // Offset relative to the formation's center (2D) or leader (3D)
    public Quaternion rotation;
}

public class PlayerController : MonoBehaviour
{
    public static PlayerController instance;

    public Camera cam;
    public LineDrawer lineDrawer;

    public List<Unit> units = new List<Unit>();
    public List<Unit> selectedUnits = new List<Unit>();

    private Vector2 startDrag;
    private Vector2 endDrag;
    private bool isDragging;
    private Texture2D selectionBoxTexture;

    private FormationMode currentMode = FormationMode.None;
    private List<FormationSlot> formationSlots = new List<FormationSlot>();
    private FormationGroup currentFormationGroup; // Keeps track of the group of units in a formation

    // Constants for 3D formation shapes
    private const float CUBE_SIZE = 1.0f; // This should match the scale of your unit cubes.

    void Awake()
    {
        instance = this;
        cam = Camera.main;
    }

    void Start()
    {
        selectionBoxTexture = new Texture2D(1, 1);
        selectionBoxTexture.SetPixel(0, 0, new Color(0, 0.5f, 0, 0.5f));
        selectionBoxTexture.Apply();

        GameObject[] unitGOs = GameObject.FindGameObjectsWithTag("Unit");
        foreach (GameObject go in unitGOs)
        {
            units.Add(go.GetComponent<Unit>());
        }
    }

    void OnGUI()
    {
        if (isDragging)
        {
            float y1 = Screen.height - startDrag.y;
            float y2 = Screen.height - Mouse.current.position.ReadValue().y;
            float x = Mathf.Min(startDrag.x, Mouse.current.position.ReadValue().x);
            float y = Mathf.Min(y1, y2);
            float width = Mathf.Abs(startDrag.x - Mouse.current.position.ReadValue().x);
            float height = Mathf.Abs(y1 - y2);
            Rect selectionRect = new Rect(x, y, width, height);
            GUI.DrawTexture(selectionRect, selectionBoxTexture);
        }
    }

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            startDrag = Mouse.current.position.ReadValue();
            isDragging = true;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
            endDrag = Mouse.current.position.ReadValue();

            if (Vector2.Distance(startDrag, endDrag) < 10f) HandleSingleSelection();
            else HandleDragSelection();
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            MoveSelectedUnits();
        }
        
        // 2D Formation mode toggle
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (selectedUnits.Count > 0)
            {
                currentMode = FormationMode.Mode2D;
                UpdateFormation();
            }
        }
        
        // 3D Formation mode toggle
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            if (selectedUnits.Count > 1) // 3D mode requires at least 2 units
            {
                currentMode = FormationMode.Mode3D;
                Update3DFormation();
            }
        }
    }

    void UpdateFormation()
    {
        if (selectedUnits.Count < 1 || currentMode != FormationMode.Mode2D)
        {
            BreakFormation();
            return;
        }

        BreakFormation(); // Reset previous formation state
        currentMode = FormationMode.Mode2D; // Ensure correct mode is set

        // Assign a new FormationGroup
        currentFormationGroup = new FormationGroup();
        currentFormationGroup.units.AddRange(selectedUnits);

        Vector3 center = Vector3.zero;
        foreach (Unit unit in selectedUnits)
        {
            center += unit.transform.position;
            unit.formationGroup = currentFormationGroup;
        }
        center /= selectedUnits.Count;

        // If only 1 or 2 units, don't form a polygon, just group loosely
        if (selectedUnits.Count < 2) 
        {
            foreach (Unit unit in selectedUnits)
            {
                unit.LockRotation();
                formationSlots.Add(new FormationSlot { unit = unit, offset = unit.transform.position - center, rotation = unit.transform.rotation });
            }
            lineDrawer.DrawLines(selectedUnits);
            return;
        }

        const float baseSideLength = 3.0f;
        int unitCount = selectedUnits.Count;
        float desiredSideLength = baseSideLength + (unitCount * 0.2f); // Side length grows with more units
        float radius = desiredSideLength / (2 * Mathf.Sin(Mathf.PI / unitCount)); // Calculate radius from side length
        float angleIncrement = 360f / unitCount;

        formationSlots.Clear(); // Clear slots for new calculation
        for (int i = 0; i < unitCount; i++)
        {
            float angleRad = Mathf.Deg2Rad * (angleIncrement * i);
            float x = Mathf.Cos(angleRad) * radius;
            float z = Mathf.Sin(angleRad) * radius;
            Vector3 targetPos = center + new Vector3(x, 0, z);
            Vector3 lookDir = (center - targetPos).normalized;
            Quaternion targetRot = Quaternion.LookRotation(lookDir);

            selectedUnits[i].MoveTo(targetPos, 3f); // Move 3x faster
            selectedUnits[i].RotateTo(lookDir);
            selectedUnits[i].LockRotation();
            formationSlots.Add(new FormationSlot { unit = selectedUnits[i], offset = targetPos - center, rotation = targetRot });
        }
        lineDrawer.DrawLines(selectedUnits);
    }

    void Update3DFormation()
    {
        if (selectedUnits.Count < 2 || currentMode != FormationMode.Mode3D) // 3D mode requires at least 2 units
        {
            BreakFormation();
            return;
        }

        BreakFormation(); // Reset previous formation state
        currentMode = FormationMode.Mode3D; // Ensure correct mode is set

        currentFormationGroup = new FormationGroup();
        currentFormationGroup.units.AddRange(selectedUnits);

        // Calculate offsets first
        List<Vector3> offsets = new List<Vector3>();
        switch (selectedUnits.Count)
        {
            case 2: // Line, cubes aligned on X-axis (face to face)
                offsets.Add(new Vector3(-CUBE_SIZE / 2, 0, 0));
                offsets.Add(new Vector3(CUBE_SIZE / 2, 0, 0));
                break;
            case 3: // Equilateral triangle, touching (as tightly as possible based on cube centers)
                float dist_center_to_vertex_3 = CUBE_SIZE / Mathf.Sqrt(3);
                offsets.Add(new Vector3(0, 0, dist_center_to_vertex_3));
                offsets.Add(new Vector3(dist_center_to_vertex_3 * Mathf.Cos(Mathf.Deg2Rad * 210), 0, dist_center_to_vertex_3 * Mathf.Sin(Mathf.Deg2Rad * 210)));
                offsets.Add(new Vector3(dist_center_to_vertex_3 * Mathf.Cos(Mathf.Deg2Rad * 330), 0, dist_center_to_vertex_3 * Mathf.Sin(Mathf.Deg2Rad * 330)));
                break;
            case 4: // Triangle base + 1 on top
                float dist_center_to_vertex_4 = CUBE_SIZE / Mathf.Sqrt(3);
                offsets.Add(new Vector3(0, 0, dist_center_to_vertex_4));
                offsets.Add(new Vector3(dist_center_to_vertex_4 * Mathf.Cos(Mathf.Deg2Rad * 210), 0, dist_center_to_vertex_4 * Mathf.Sin(Mathf.Deg2Rad * 210)));
                offsets.Add(new Vector3(dist_center_to_vertex_4 * Mathf.Cos(Mathf.Deg2Rad * 330), 0, dist_center_to_vertex_4 * Mathf.Sin(Mathf.Deg2Rad * 330)));
                offsets.Add(new Vector3(0, CUBE_SIZE, 0));
                break;
            case 5: // Square Pyramid
                offsets.Add(new Vector3(-CUBE_SIZE / 2, 0, -CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, 0, -CUBE_SIZE / 2));
                offsets.Add(new Vector3(-CUBE_SIZE / 2, 0, CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, 0, CUBE_SIZE / 2));
                offsets.Add(new Vector3(0, CUBE_SIZE, 0));
                break;
            case 6: // Square base + 2 on top (line)
                offsets.Add(new Vector3(-CUBE_SIZE / 2, 0, -CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, 0, -CUBE_SIZE / 2));
                offsets.Add(new Vector3(-CUBE_SIZE / 2, 0, CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, 0, CUBE_SIZE / 2));
                offsets.Add(new Vector3(-CUBE_SIZE / 2, CUBE_SIZE, 0)); offsets.Add(new Vector3(CUBE_SIZE / 2, CUBE_SIZE, 0));
                break;
            case 7: // Square base + 3 on top (triangle)
                offsets.Add(new Vector3(-CUBE_SIZE / 2, 0, -CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, 0, -CUBE_SIZE / 2));
                offsets.Add(new Vector3(-CUBE_SIZE / 2, 0, CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, 0, CUBE_SIZE / 2));
                float dist_center_to_vertex_7 = CUBE_SIZE / Mathf.Sqrt(3);
                offsets.Add(new Vector3(0, CUBE_SIZE, dist_center_to_vertex_7));
                offsets.Add(new Vector3(dist_center_to_vertex_7 * Mathf.Cos(Mathf.Deg2Rad * 210), CUBE_SIZE, dist_center_to_vertex_7 * Mathf.Sin(Mathf.Deg2Rad * 210)));
                offsets.Add(new Vector3(dist_center_to_vertex_7 * Mathf.Cos(Mathf.Deg2Rad * 330), CUBE_SIZE, dist_center_to_vertex_7 * Mathf.Sin(Mathf.Deg2Rad * 330)));
                break;
            case 8: // Full Cube (2x2x2)
                offsets.Add(new Vector3(-CUBE_SIZE / 2, 0, -CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, 0, -CUBE_SIZE / 2));
                offsets.Add(new Vector3(-CUBE_SIZE / 2, 0, CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, 0, CUBE_SIZE / 2));
                offsets.Add(new Vector3(-CUBE_SIZE / 2, CUBE_SIZE, -CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, CUBE_SIZE, -CUBE_SIZE / 2));
                offsets.Add(new Vector3(-CUBE_SIZE / 2, CUBE_SIZE, CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, CUBE_SIZE, CUBE_SIZE / 2));
                break;
        }

        // Determine the formation's center based on selected units' current positions
        Vector3 formationCurrentCenter = Vector3.zero;
        foreach (Unit unit in selectedUnits)
        {
            formationCurrentCenter += unit.transform.position;
        }
        formationCurrentCenter /= selectedUnits.Count;
        formationCurrentCenter.y = CUBE_SIZE / 2.0f; // Base of formation on ground

        Unit leaderUnit = selectedUnits[0]; // First selected unit is the leader
        leaderUnit.IsLeader = true;
        leaderUnit.EnableNavMeshAgent(true); // Leader uses NavMeshAgent
        leaderUnit.SetNavMeshAgentControl(true); // Leader's NavMeshAgent controls its movement

        for (int i = 0; i < selectedUnits.Count; i++)
        {
            Unit unit = selectedUnits[i];
            unit.formationGroup = currentFormationGroup;

            if (unit != leaderUnit) // If not the leader, it's a follower
            {
                unit.EnableNavMeshAgent(false); // Disable NavMeshAgent for followers
                unit.SetNavMeshAgentControl(false); // Disable agent control for followers
                
                // Calculate offset relative to the leader's *initial* position in the formation
                Vector3 offsetFromLeader = offsets[i] - offsets[selectedUnits.IndexOf(leaderUnit)];
                unit.SetLeader(leaderUnit, offsetFromLeader);
            }
            
            // Initial placement for all units (including leader)
            Vector3 targetPos = formationCurrentCenter + offsets[i];
            unit.MoveTo(targetPos, 3f); // Use MoveTo for initial "climbing" animation
            unit.LockRotation(); // Lock rotation for all units in formation
            unit.transform.rotation = Quaternion.identity; // Align to world axes
            formationSlots.Add(new FormationSlot { unit = unit, offset = offsets[i], rotation = Quaternion.identity });
        }
    }
    
    void Release3DFormation() // Changed back to a regular method
    {
        Vector3 formationCenter = Vector3.zero;
        List<Unit> unitsInFormation = new List<Unit>();

        foreach (var slot in formationSlots)
        {
            if(slot.unit != null)
            {
                unitsInFormation.Add(slot.unit);
                formationCenter += slot.unit.transform.position; // Summing up current positions
            }
        }
        formationCenter /= unitsInFormation.Count;
        formationCenter.y = 0; // Ensure the center is on the ground

        float scatterDistance = 1.0f; // Distance to scatter each cube outwards

        foreach (Unit unit in unitsInFormation)
        {
            // Instant drop to ground
            Vector3 groundPos = new Vector3(unit.transform.position.x, 0, unit.transform.position.z);
            unit.transform.position = groundPos;

            // Re-enable NavMeshAgent and trigger avoidance
            unit.EnableNavMeshAgent(true);
            unit.SetNavMeshAgentControl(true);
            unit.UnlockRotation();
            unit.ClearLeader();
            unit.IsLeader = false;

            // Calculate scatter target: move away from the formation center
            Vector3 directionFromCenter = (groundPos - formationCenter).normalized;
            Vector3 scatterTarget = groundPos + directionFromCenter * scatterDistance;

            // Give a command to move to the scatter target
            // This will trigger NavMeshAgent avoidance and create an outward burst effect.
            unit.MoveTo(scatterTarget, 2f); // Move at a moderate speed
        }
        formationSlots.Clear(); // Clear slots immediately after processing
    }

    void MoveSelectedUnits()
    {
        if (selectedUnits.Count == 0) return;
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            if (currentMode == FormationMode.Mode2D)
            {
                Vector3 newCenter = hit.point;
                foreach (var slot in formationSlots)
                {
                    Vector3 targetPos = newCenter + slot.offset;
                    slot.unit.MoveTo(targetPos);
                    slot.unit.transform.rotation = slot.rotation;
                }
            }
            else if (currentMode == FormationMode.Mode3D)
            {
                Vector3 newTargetForLeader = hit.point;
                // Adjust click point so base of formation is on ground
                newTargetForLeader.y = CUBE_SIZE / 2.0f; 

                Unit leader = null;
                foreach (Unit unit in selectedUnits)
                {
                    if (unit.IsLeader)
                    {
                        leader = unit;
                        break;
                    }
                }

                if (leader != null)
                {
                    leader.MoveTo(newTargetForLeader); // Leader moves via NavMeshAgent
                    // Follower units will update their positions relative to the leader in their Update method
                }
            }
            else // Not in a formation, default circular spread
            {
                int unitCount = selectedUnits.Count;
                float angle = 360f / unitCount;
                float radius = 1f;

                for (int i = 0; i < unitCount; i++)
                {
                    float x = Mathf.Cos(angle * i * Mathf.Deg2Rad) * radius;
                    float z = Mathf.Sin(angle * i * Mathf.Deg2Rad) * radius;
                    Vector3 targetPos = hit.point + new Vector3(x, 0, z);
                    selectedUnits[i].MoveTo(targetPos);
                }
            }
        }
    }

    void HandleSingleSelection()
    {
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            Unit unit = hit.collider.GetComponent<Unit>();
            if (unit != null)
            {
                if (!Keyboard.current.shiftKey.isPressed && !Keyboard.current.leftCtrlKey.isPressed) ClearSelection();
                if (selectedUnits.Contains(unit))
                {
                    if (Keyboard.current.leftCtrlKey.isPressed) DeselectUnit(unit);
                }
                else SelectUnit(unit);
            }
            else
            {
                if (!Keyboard.current.shiftKey.isPressed && !Keyboard.current.leftCtrlKey.isPressed) ClearSelection();
            }
        }
        else
        {
            if (!Keyboard.current.shiftKey.isPressed && !Keyboard.current.leftCtrlKey.isPressed) ClearSelection();
        }

        if (currentMode == FormationMode.Mode2D && selectedUnits.Count > 0) UpdateFormation();
        else if (currentMode == FormationMode.Mode3D && selectedUnits.Count > 1) Update3DFormation();
    }

    void HandleDragSelection()
    {
        if (!Keyboard.current.shiftKey.isPressed && !Keyboard.current.leftCtrlKey.isPressed)
        {
            ClearSelection();
        }

        // Corrected Rect calculation for OnGUI
        float x1 = startDrag.x;
        float y1 = Screen.height - startDrag.y; // Invert Y for GUI space
        float x2 = Mouse.current.position.ReadValue().x;
        float y2 = Screen.height - Mouse.current.position.ReadValue().y; // Invert Y for GUI space

        float rectX = Mathf.Min(x1, x2);
        float rectY = Mathf.Min(y1, y2);
        float rectWidth = Mathf.Abs(x1 - x2);
        float rectHeight = Mathf.Abs(y1 - y2);
        Rect selectionRect = new Rect(rectX, rectY, rectWidth, rectHeight);


        foreach (Unit unit in units)
        {
            Vector3 screenPos = cam.WorldToScreenPoint(unit.transform.position);
            // screenPos.z > 0 check to ensure unit is in front of camera
            if (screenPos.z > 0 && selectionRect.Contains(new Vector2(screenPos.x, Screen.height - screenPos.y)))
            {
                if (Keyboard.current.leftCtrlKey.isPressed)
                {
                    if (selectedUnits.Contains(unit)) DeselectUnit(unit);
                    else SelectUnit(unit);
                }
                else SelectUnit(unit);
            }
        }
        if (currentMode == FormationMode.Mode2D && selectedUnits.Count > 0) UpdateFormation();
        else if (currentMode == FormationMode.Mode3D && selectedUnits.Count > 1) Update3DFormation();
    }

    void SelectUnit(Unit unit)
    {
        if (!selectedUnits.Contains(unit))
        {
            selectedUnits.Add(unit);
            unit.ToggleSelection(true);
        }
    }

    void DeselectUnit(Unit unit)
    {
        if (selectedUnits.Contains(unit))
        {
            selectedUnits.Remove(unit);
            unit.ToggleSelection(false);
        }
    }

    void ClearSelection()
    {
        foreach (Unit unit in selectedUnits)
        {
            unit.ToggleSelection(false);
        }
        selectedUnits.Clear();
        BreakFormation();
    }

    void BreakFormation()
    {
        if (currentMode == FormationMode.Mode3D)
        {
            Release3DFormation(); // Handle 3D specific release (instant drop, re-enable NavMeshAgent, nudge)
        }
        else // For 2D mode or no formation
        {
            if (currentFormationGroup != null)
            {
                foreach (Unit unit in currentFormationGroup.units)
                {
                    if (unit != null)
                    {
                        unit.UnlockRotation();
                        unit.formationGroup = null;
                        unit.EnableNavMeshAgent(true); // Re-enable NavMeshAgent
                        unit.SetNavMeshAgentControl(true); // Re-enable agent control
                        unit.ClearLeader(); // Clear leader reference
                        unit.IsLeader = false; // Reset leader status
                    }
                }
                currentFormationGroup = null;
            }
            formationSlots.Clear(); // Clear for 2D/None
        }
        
        currentMode = FormationMode.None;

        if (lineDrawer != null)
        {
            lineDrawer.ClearLines();
        }
    }
}