using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;

public enum FormationMode { None, Mode2D, Mode3D, LadderMode }

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
                BreakFormation(true);
                currentMode = FormationMode.Mode2D;
                UpdateFormation();
            }
        }
        
        // 3D Formation mode toggle
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            if (selectedUnits.Count > 1) // 3D mode requires at least 2 units
            {
                BreakFormation(true);
                currentMode = FormationMode.Mode3D;
                Update3DFormation();
            }
        }
        
        // Ladder Formation mode toggle
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            if (selectedUnits.Count > 0)
            {
                BreakFormation(true);
                currentMode = FormationMode.LadderMode;
                UpdateLadderFormation();
            }
        }
    }

    void UpdateFormation()
    {
        if (selectedUnits.Count < 1) return;

        currentFormationGroup = new FormationGroup();
        currentFormationGroup.units.AddRange(selectedUnits);

        Vector3 center = Vector3.zero;
        foreach (Unit unit in selectedUnits)
        {
            center += unit.transform.position;
            unit.formationGroup = currentFormationGroup;
        }
        center /= selectedUnits.Count;

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
        float desiredSideLength = baseSideLength + (unitCount * 0.2f);
        float radius = desiredSideLength / (2 * Mathf.Sin(Mathf.PI / unitCount));
        float angleIncrement = 360f / unitCount;

        formationSlots.Clear();
        for (int i = 0; i < unitCount; i++)
        {
            float angleRad = Mathf.Deg2Rad * (angleIncrement * i);
            float x = Mathf.Cos(angleRad) * radius;
            float z = Mathf.Sin(angleRad) * radius;
            Vector3 targetPos = center + new Vector3(x, 0, z);
            Vector3 lookDir = (center - targetPos).normalized;
            Quaternion targetRot = Quaternion.LookRotation(lookDir);

            selectedUnits[i].MoveTo(targetPos, 3f);
            selectedUnits[i].RotateTo(lookDir);
            selectedUnits[i].LockRotation();
            formationSlots.Add(new FormationSlot { unit = selectedUnits[i], offset = targetPos - center, rotation = targetRot });
        }
        lineDrawer.DrawLines(selectedUnits);
    }

    void Update3DFormation()
    {
        if (selectedUnits.Count < 2) return;

        currentFormationGroup = new FormationGroup();
        currentFormationGroup.units.AddRange(selectedUnits);

        List<Vector3> offsets = new List<Vector3>();
        switch (selectedUnits.Count)
        {
            case 2:
                offsets.Add(new Vector3(-CUBE_SIZE / 2, 0, 0));
                offsets.Add(new Vector3(CUBE_SIZE / 2, 0, 0));
                break;
            case 3:
                float dist_center_to_vertex_3 = CUBE_SIZE / Mathf.Sqrt(3);
                offsets.Add(new Vector3(0, 0, dist_center_to_vertex_3));
                offsets.Add(new Vector3(dist_center_to_vertex_3 * Mathf.Cos(Mathf.Deg2Rad * 210), 0, dist_center_to_vertex_3 * Mathf.Sin(Mathf.Deg2Rad * 210)));
                offsets.Add(new Vector3(dist_center_to_vertex_3 * Mathf.Cos(Mathf.Deg2Rad * 330), 0, dist_center_to_vertex_3 * Mathf.Sin(Mathf.Deg2Rad * 330)));
                break;
            case 4:
                float dist_center_to_vertex_4 = CUBE_SIZE / Mathf.Sqrt(3);
                offsets.Add(new Vector3(0, 0, dist_center_to_vertex_4));
                offsets.Add(new Vector3(dist_center_to_vertex_4 * Mathf.Cos(Mathf.Deg2Rad * 210), 0, dist_center_to_vertex_4 * Mathf.Sin(Mathf.Deg2Rad * 210)));
                offsets.Add(new Vector3(dist_center_to_vertex_4 * Mathf.Cos(Mathf.Deg2Rad * 330), 0, dist_center_to_vertex_4 * Mathf.Sin(Mathf.Deg2Rad * 330)));
                offsets.Add(new Vector3(0, CUBE_SIZE, 0));
                break;
            case 5:
                offsets.Add(new Vector3(-CUBE_SIZE / 2, 0, -CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, 0, -CUBE_SIZE / 2));
                offsets.Add(new Vector3(-CUBE_SIZE / 2, 0, CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, 0, CUBE_SIZE / 2));
                offsets.Add(new Vector3(0, CUBE_SIZE, 0));
                break;
            case 6:
                offsets.Add(new Vector3(-CUBE_SIZE / 2, 0, -CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, 0, -CUBE_SIZE / 2));
                offsets.Add(new Vector3(-CUBE_SIZE / 2, 0, CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, 0, CUBE_SIZE / 2));
                offsets.Add(new Vector3(-CUBE_SIZE / 2, CUBE_SIZE, 0)); offsets.Add(new Vector3(CUBE_SIZE / 2, CUBE_SIZE, 0));
                break;
            case 7:
                offsets.Add(new Vector3(-CUBE_SIZE / 2, 0, -CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, 0, -CUBE_SIZE / 2));
                offsets.Add(new Vector3(-CUBE_SIZE / 2, 0, CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, 0, CUBE_SIZE / 2));
                float dist_center_to_vertex_7 = CUBE_SIZE / Mathf.Sqrt(3);
                offsets.Add(new Vector3(0, CUBE_SIZE, dist_center_to_vertex_7));
                offsets.Add(new Vector3(dist_center_to_vertex_7 * Mathf.Cos(Mathf.Deg2Rad * 210), CUBE_SIZE, dist_center_to_vertex_7 * Mathf.Sin(Mathf.Deg2Rad * 210)));
                offsets.Add(new Vector3(dist_center_to_vertex_7 * Mathf.Cos(Mathf.Deg2Rad * 330), CUBE_SIZE, dist_center_to_vertex_7 * Mathf.Sin(Mathf.Deg2Rad * 330)));
                break;
            case 8:
                offsets.Add(new Vector3(-CUBE_SIZE / 2, 0, -CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, 0, -CUBE_SIZE / 2));
                offsets.Add(new Vector3(-CUBE_SIZE / 2, 0, CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, 0, CUBE_SIZE / 2));
                offsets.Add(new Vector3(-CUBE_SIZE / 2, CUBE_SIZE, -CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, CUBE_SIZE, -CUBE_SIZE / 2));
                offsets.Add(new Vector3(-CUBE_SIZE / 2, CUBE_SIZE, CUBE_SIZE / 2)); offsets.Add(new Vector3(CUBE_SIZE / 2, CUBE_SIZE, CUBE_SIZE / 2));
                break;
        }

        Vector3 formationCurrentCenter = Vector3.zero;
        foreach (Unit unit in selectedUnits)
        {
            formationCurrentCenter += unit.transform.position;
        }
        formationCurrentCenter /= selectedUnits.Count;
        formationCurrentCenter.y = CUBE_SIZE / 2.0f;

        Unit leaderUnit = selectedUnits[0];
        leaderUnit.IsLeader = true;
        leaderUnit.EnableNavMeshAgent(true);
        leaderUnit.SetNavMeshAgentControl(true);

        for (int i = 0; i < selectedUnits.Count; i++)
        {
            Unit unit = selectedUnits[i];
            unit.formationGroup = currentFormationGroup;

            if (unit != leaderUnit)
            {
                unit.EnableNavMeshAgent(false);
                unit.SetNavMeshAgentControl(false);
                
                Vector3 offsetFromLeader = offsets[i] - offsets[selectedUnits.IndexOf(leaderUnit)];
                unit.SetLeader(leaderUnit, offsetFromLeader);
            }
            
            Vector3 targetPos = formationCurrentCenter + offsets[i];
            unit.MoveTo(targetPos, 3f);
            unit.LockRotation();
            unit.transform.rotation = Quaternion.identity;
            formationSlots.Add(new FormationSlot { unit = unit, offset = offsets[i], rotation = Quaternion.identity });
        }
    }

    void UpdateLadderFormation()
    {
        if (selectedUnits.Count < 1) return;

        currentFormationGroup = new FormationGroup();
        currentFormationGroup.units.AddRange(selectedUnits);

        Vector3 formationCurrentCenter = Vector3.zero;
        foreach (Unit unit in selectedUnits)
        {
            formationCurrentCenter += unit.transform.position;
        }
        formationCurrentCenter /= selectedUnits.Count;
        formationCurrentCenter.y = CUBE_SIZE / 2.0f;

        Unit leaderUnit = selectedUnits[0];
        leaderUnit.IsLeader = true;
        leaderUnit.EnableNavMeshAgent(true);
        leaderUnit.SetNavMeshAgentControl(true);

        formationSlots.Clear();
        for (int i = 0; i < selectedUnits.Count; i++)
        {
            Unit unit = selectedUnits[i];
            unit.formationGroup = currentFormationGroup;

            Vector3 offset = new Vector3(0, i * CUBE_SIZE, 0);
            
            if (unit != leaderUnit)
            {
                unit.EnableNavMeshAgent(false);
                unit.SetNavMeshAgentControl(false);
                unit.SetLeader(leaderUnit, offset); 
            }
            
            Vector3 targetPos = formationCurrentCenter + offset;
            unit.MoveTo(targetPos, 2f);
            unit.LockRotation();
            unit.transform.rotation = Quaternion.identity;
            formationSlots.Add(new FormationSlot { unit = unit, offset = offset, rotation = Quaternion.identity });
        }
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
            else if (currentMode == FormationMode.Mode3D || currentMode == FormationMode.LadderMode)
            {
                Vector3 newTargetForLeader = hit.point;
                newTargetForLeader.y = CUBE_SIZE / 2.0f; 

                Unit leader = selectedUnits.Find(u => u.IsLeader);
                if (leader == null && selectedUnits.Count > 0) leader = selectedUnits[0];

                if (leader != null)
                {
                    leader.MoveTo(newTargetForLeader);
                }
            }
            else
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

        if (currentMode == FormationMode.Mode2D && selectedUnits.Count > 0)
        {
            BreakFormation(true);
            UpdateFormation();
        }
        else if (currentMode == FormationMode.Mode3D && selectedUnits.Count > 1)
        {
            BreakFormation(true);
            Update3DFormation();
        }
    }

    void HandleDragSelection()
    {
        if (!Keyboard.current.shiftKey.isPressed && !Keyboard.current.leftCtrlKey.isPressed)
        {
            ClearSelection();
        }

        float x1 = startDrag.x;
        float y1 = Screen.height - startDrag.y;
        float x2 = Mouse.current.position.ReadValue().x;
        float y2 = Screen.height - Mouse.current.position.ReadValue().y;

        float rectX = Mathf.Min(x1, x2);
        float rectY = Mathf.Min(y1, y2);
        float rectWidth = Mathf.Abs(x1 - x2);
        float rectHeight = Mathf.Abs(y1 - y2);
        Rect selectionRect = new Rect(rectX, rectY, rectWidth, rectHeight);

        foreach (Unit unit in units)
        {
            Vector3 screenPos = cam.WorldToScreenPoint(unit.transform.position);
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
        if (currentMode == FormationMode.Mode2D && selectedUnits.Count > 0)
        {
            BreakFormation(true);
            UpdateFormation();
        }
        else if (currentMode == FormationMode.Mode3D && selectedUnits.Count > 1)
        {
            BreakFormation(true);
            Update3DFormation();
        }
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
        BreakFormation(false);
    }

    void BreakFormation(bool forNewFormation = false)
    {
        if (currentMode == FormationMode.None) return;

        List<Unit> unitsToProcess = new List<Unit>();
        if (currentFormationGroup != null)
        {
            unitsToProcess.AddRange(currentFormationGroup.units);
        }

        if (currentMode == FormationMode.Mode3D || currentMode == FormationMode.LadderMode)
        {
            if (forNewFormation)
            {
                foreach (Unit unit in unitsToProcess)
                {
                    if (unit == null) continue;
                    unit.StopAllCoroutines();
                    Vector3 groundPos = new Vector3(unit.transform.position.x, 0.5f, unit.transform.position.z);
                    unit.transform.position = groundPos;
                    
                    unit.EnableNavMeshAgent(true);
                    unit.SetNavMeshAgentControl(true);
                    unit.UnlockRotation();
                    unit.ClearLeader();
                    unit.IsLeader = false;
                    unit.formationGroup = null;
                }
            }
            else
            {
                Vector3 formationCenter = Vector3.zero;
                if (unitsToProcess.Count > 0)
                {
                    foreach(var unit in unitsToProcess) formationCenter += unit.transform.position;
                    formationCenter /= unitsToProcess.Count;
                }
                formationCenter.y = 0.5f;

                float scatterDistance = 1.0f;
                int unitCount = unitsToProcess.Count;
                if (unitCount == 0) return;
                float angleIncrement = 360f / unitCount;

                for (int i = 0; i < unitCount; i++)
                {
                    Unit unit = unitsToProcess[i];
                    if (unit == null) continue;
                    
                    float angleRad = Mathf.Deg2Rad * (angleIncrement * i);
                    float x = Mathf.Cos(angleRad) * scatterDistance;
                    float z = Mathf.Sin(angleRad) * scatterDistance;
                    Vector3 scatterTarget = formationCenter + new Vector3(x, 0, z);

                    StartCoroutine(unit.FallAndScatter(scatterTarget, 4f));
                }
            }
        }
        else if (currentMode == FormationMode.Mode2D)
        {
            foreach (Unit unit in unitsToProcess)
            {
                if (unit != null)
                {
                    unit.UnlockRotation();
                    unit.formationGroup = null;
                }
            }
        }
        
        if (currentFormationGroup != null)
        {
            currentFormationGroup.units.Clear();
            currentFormationGroup = null;
        }
        formationSlots.Clear();
        currentMode = FormationMode.None;

        if (lineDrawer != null)
        {
            lineDrawer.ClearLines();
        }
    }
}