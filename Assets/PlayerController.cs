using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using System.Linq;

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
    private FormationGroup currentFormationGroup;
    private GameObject currentFormationParent;
    private bool isSwitchingFormation = false;
    private bool isExecutingSkill = false;

    private const float CUBE_SIZE = 1.0f;

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
        if (isSwitchingFormation || isExecutingSkill) return;

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
        
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (selectedUnits.Count > 0)
            {
                StartCoroutine(SwitchFormation(FormationMode.Mode2D));
            }
        }
        
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            if (selectedUnits.Count > 1)
            {
                StartCoroutine(SwitchFormation(FormationMode.Mode3D));
            }
        }
        
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            if (selectedUnits.Count > 0)
            {
                StartCoroutine(SwitchFormation(FormationMode.LadderMode));
            }
        }

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            ExecuteFormationSkill();
        }
    }

    void ExecuteFormationSkill()
    {
        if (selectedUnits.Count == 0) return;

        switch (currentMode)
        {
            case FormationMode.Mode2D:
                StartCoroutine(ExecuteMode2DSkill());
                break;
            case FormationMode.Mode3D:
                Debug.Log("Mode 3D Skill Activated (Not Implemented)");
                break;
            case FormationMode.LadderMode:
                StartCoroutine(ExecuteLadderModeSkill());
                break;
        }
    }

    IEnumerator ExecuteLadderModeSkill()
    {
        isExecutingSkill = true;

        Unit topCube = null;
        float max_y = float.MinValue;

        if (currentFormationParent != null)
        {
            foreach (Transform child in currentFormationParent.transform)
            {
                if (child.localPosition.y > max_y)
                {
                    max_y = child.localPosition.y;
                    topCube = child.GetComponent<Unit>();
                }
            }
        }

        if (topCube == null)
        {
            isExecutingSkill = false;
            yield break;
        }

        // 1. Calculate the top cube's target world position
        Vector3 topCubeTargetWorldPos = topCube.transform.position + topCube.transform.forward * CUBE_SIZE;

        // 2. Un-parent the top cube
        topCube.transform.parent = null;

        // 3. Start the new animation coroutine and wait for it
        yield return StartCoroutine(topCube.MoveForwardAndFall(topCubeTargetWorldPos, 15f, 20f));

        // 4. Deselect the unit after its movement is complete
        DeselectUnit(topCube);

        isExecutingSkill = false;
    }

    IEnumerator ExecuteMode2DSkill()
    {
        isExecutingSkill = true;

        // Stop all units before starting the skill
        foreach (var slot in formationSlots)
        {
            if (slot.unit != null)
            {
                slot.unit.StopMovement();
            }
        }

        // Wait for one frame to ensure agents have stopped before issuing a new command
        yield return null;

        // 1. Calculate center and count
        Vector3 center = Vector3.zero;
        int unitCount = 0;
        foreach (var slot in formationSlots)
        {
            if (slot.unit != null)
            {
                center += slot.unit.transform.position;
                unitCount++;
            }
        }
        if (unitCount > 0) center /= unitCount;

        // 2. Calculate dynamic values based on unit count using ratio-based scaling
        const float baseUnitCount = 8f;
        const float baseWaitTime = 0.45f;
        const float baseSpeed = 22.5f;
        
        float scalingUnitCount = Mathf.Clamp(unitCount, 3, 8);
        float scalingFactor = scalingUnitCount / baseUnitCount;

        float dynamicWaitTime = baseWaitTime * scalingFactor;
        float dynamicSpreadSpeed = baseSpeed * scalingFactor;
        float gatherSpeed = 7.5f;

        // 3. Gather phase
        foreach (var slot in formationSlots)
        {
            if (slot.unit != null)
            {
                slot.unit.MoveTo(center, gatherSpeed); // Start moving towards center
            }
        }

        // Wait a dynamic time for them to gather
        yield return new WaitForSeconds(dynamicWaitTime); 

        // 4. Spread phase
        List<Coroutine> spreadCoroutines = new List<Coroutine>();
        foreach (var slot in formationSlots)
        {
            if (slot.unit != null)
            {
                Vector3 originalPos = center + slot.offset;
                spreadCoroutines.Add(slot.unit.MoveTo(originalPos, dynamicSpreadSpeed));
            }
        }

        // Wait for all units to spread back out to their positions
        foreach (var co in spreadCoroutines)
        {
            if (co != null) yield return co;
        }

        isExecutingSkill = false;
    }


    IEnumerator SwitchFormation(FormationMode newMode)
    {
        if (isSwitchingFormation || currentMode == newMode) yield break;

        isSwitchingFormation = true;

        FormationMode previousMode = currentMode;
        CleanupPreviousFormation();

        currentMode = newMode;
        List<Coroutine> formationCoroutines = new List<Coroutine>();
        switch (newMode)
        {
            case FormationMode.Mode2D:
                formationCoroutines = UpdateFormation(previousMode);
                break;
            case FormationMode.Mode3D:
                formationCoroutines = Update3DFormation(previousMode);
                break;
            case FormationMode.LadderMode:
                formationCoroutines = UpdateLadderFormation(previousMode);
                break;
        }

        if (formationCoroutines != null)
        {
            foreach (var co in formationCoroutines)
            {
                if (co != null) yield return co;
            }
        }

        isSwitchingFormation = false;
    }

    void CleanupPreviousFormation()
    {
        FormationMode modeToBreak = currentMode;

        if (modeToBreak == FormationMode.Mode3D || modeToBreak == FormationMode.LadderMode)
        {
            if (currentFormationParent != null)
            {
                List<Unit> unitsToUnparent = new List<Unit>();
                foreach (Transform child in currentFormationParent.transform)
                {
                    Unit unit = child.GetComponent<Unit>();
                    if (unit != null) unitsToUnparent.Add(unit);
                }

                foreach (var unit in unitsToUnparent)
                {
                    unit.transform.parent = null;
                    unit.EnableNavMeshAgent(true);
                    unit.UnlockRotation();
                    unit.ClearLeader();
                    unit.IsLeader = false;
                }
                
                Destroy(currentFormationParent);
                currentFormationParent = null;
            }
        }
        else if (modeToBreak == FormationMode.Mode2D)
        {
            if (currentFormationGroup != null)
            {
                foreach (var unit in currentFormationGroup.units)
                {
                    if (unit != null)
                    {
                        unit.UnlockRotation();
                        unit.formationGroup = null;
                    }
                }
            }
        }
        
        if (currentFormationGroup != null)
        {
            currentFormationGroup.units.Clear();
            currentFormationGroup = null;
        }
        formationSlots.Clear();
        if (lineDrawer != null) lineDrawer.ClearLines();

        currentMode = FormationMode.None;
    }

    List<Coroutine> UpdateFormation(FormationMode previousMode = FormationMode.None)
    {
        if (selectedUnits.Count < 1) return new List<Coroutine>();

        var coroutines = new List<Coroutine>();
        currentFormationGroup = new FormationGroup();
        currentFormationGroup.units.AddRange(selectedUnits);

        Vector3 center = Vector3.zero;
        foreach (Unit unit in selectedUnits)
        {
            center += unit.transform.position;
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
            return coroutines;
        }

        const float baseSideLength = 3.0f;
        int unitCount = selectedUnits.Count;
        float desiredSideLength = baseSideLength + (unitCount * 0.2f);
        float radius = desiredSideLength / (2 * Mathf.Sin(Mathf.PI / unitCount));
        float angleIncrement = 360f / unitCount;

        bool useFallAnimation = (previousMode == FormationMode.Mode3D || previousMode == FormationMode.LadderMode);

        formationSlots.Clear();
        for (int i = 0; i < unitCount; i++)
        {
            float angleRad = Mathf.Deg2Rad * (angleIncrement * i);
            float x = Mathf.Cos(angleRad) * radius;
            float z = Mathf.Sin(angleRad) * radius;
            Vector3 targetPos = center + new Vector3(x, 0, z);
            Vector3 lookDir = (center - targetPos).normalized;
            Quaternion targetRot = Quaternion.LookRotation(lookDir);

            Unit currentUnit = selectedUnits[i];
            if (useFallAnimation)
            {
                Vector3 fallTarget = new Vector3(targetPos.x, currentUnit.transform.position.y, targetPos.z);
                fallTarget.y = CUBE_SIZE / 2.0f;
                coroutines.Add(StartCoroutine(currentUnit.FallAndScatter(fallTarget, 4f)));
            }
            else
            {
                coroutines.Add(currentUnit.MoveTo(targetPos, 3f));
            }

            coroutines.Add(currentUnit.RotateTo(lookDir));
            // currentUnit.LockRotation(); // Removed to allow NavMeshAgent to handle rotation
            formationSlots.Add(new FormationSlot { unit = selectedUnits[i], offset = targetPos - center, rotation = targetRot });
        }
        lineDrawer.DrawLines(selectedUnits);
        return coroutines;
    }

    List<Vector3> Get3DFormationOffsets(int count)
    {
        List<Vector3> offsets = new List<Vector3>();
        switch (count)
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
        return offsets;
    }

    IEnumerator MoveToLocalPosition(Transform target, Vector3 localPosition, float duration)
    {
        float time = 0;
        Vector3 startPosition = target.localPosition;
        while (time < duration)
        {
            target.localPosition = Vector3.Lerp(startPosition, localPosition, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
        target.localPosition = localPosition;
    }

    List<Coroutine> Update3DFormation(FormationMode previousMode = FormationMode.None)
    {
        if (selectedUnits.Count < 2) return new List<Coroutine>();

        var coroutines = new List<Coroutine>();
        Vector3 formationCenter = Vector3.zero;
        foreach (Unit unit in selectedUnits)
        {
            formationCenter += unit.transform.position;
        }
        formationCenter /= selectedUnits.Count;

        currentFormationParent = new GameObject("FormationParent");
        currentFormationParent.transform.position = formationCenter;
        
        NavMeshAgent parentAgent = currentFormationParent.AddComponent<NavMeshAgent>();
        if (selectedUnits.Count > 0 && selectedUnits[0] != null)
        {
            NavMeshAgent firstUnitAgent = selectedUnits[0].GetComponent<NavMeshAgent>();
            parentAgent.speed = firstUnitAgent.speed;
            parentAgent.angularSpeed = firstUnitAgent.angularSpeed;
            parentAgent.acceleration = firstUnitAgent.acceleration;
            parentAgent.stoppingDistance = firstUnitAgent.stoppingDistance;
            parentAgent.radius = firstUnitAgent.radius;
        }

        List<Vector3> offsets = Get3DFormationOffsets(selectedUnits.Count);

        for (int i = 0; i < selectedUnits.Count; i++)
        {
            Unit unit = selectedUnits[i];
            Vector3 offset = offsets[i];

            if (Mathf.Approximately(offset.y, 0))
            {
                offset.y += CUBE_SIZE / 2.0f;
            }

            unit.StopAllCoroutines();
            unit.EnableNavMeshAgent(false);
            unit.transform.parent = currentFormationParent.transform;
            
            coroutines.Add(StartCoroutine(MoveToLocalPosition(unit.transform, offset, 0.5f)));

            unit.LockRotation();
            unit.transform.localRotation = Quaternion.identity;
        }
        return coroutines;
    }

    List<Coroutine> UpdateLadderFormation(FormationMode previousMode = FormationMode.None)
    {
        if (selectedUnits.Count < 1) return new List<Coroutine>();

        var coroutines = new List<Coroutine>();
        Vector3 formationCenter = Vector3.zero;
        foreach (Unit unit in selectedUnits)
        {
            formationCenter += unit.transform.position;
        }
        formationCenter /= selectedUnits.Count;
        formationCenter.y = 0.0f; // Set parent to ground level

        currentFormationParent = new GameObject("LadderParent");
        currentFormationParent.transform.position = formationCenter;
        
        NavMeshAgent parentAgent = currentFormationParent.AddComponent<NavMeshAgent>();
        if (selectedUnits.Count > 0 && selectedUnits[0] != null)
        {
            NavMeshAgent firstUnitAgent = selectedUnits[0].GetComponent<NavMeshAgent>();
            parentAgent.speed = firstUnitAgent.speed;
            parentAgent.angularSpeed = firstUnitAgent.angularSpeed;
            parentAgent.acceleration = firstUnitAgent.acceleration;
            parentAgent.stoppingDistance = firstUnitAgent.stoppingDistance;
            parentAgent.radius = firstUnitAgent.radius;
        }

        for (int i = 0; i < selectedUnits.Count; i++)
        {
            Unit unit = selectedUnits[i];
            Vector3 offset = new Vector3(0, (i * CUBE_SIZE) + (CUBE_SIZE / 2.0f), 0);

            unit.StopAllCoroutines();
            unit.EnableNavMeshAgent(false);
            unit.transform.parent = currentFormationParent.transform;
            
            coroutines.Add(StartCoroutine(MoveToLocalPosition(unit.transform, offset, 0.5f)));

            unit.LockRotation();
            unit.transform.localRotation = Quaternion.identity;
        }
        return coroutines;
    }

    void MoveSelectedUnits()
    {
        if (selectedUnits.Count == 0) return;
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            if (currentMode == FormationMode.Mode3D || currentMode == FormationMode.LadderMode)
            {
                if (currentFormationParent != null)
                {
                    NavMeshAgent parentAgent = currentFormationParent.GetComponent<NavMeshAgent>();
                    if (parentAgent != null)
                    {
                        parentAgent.SetDestination(hit.point);
                    }
                }
            }
            else if (currentMode == FormationMode.Mode2D)
            {
                Vector3 newCenter = hit.point;
                foreach (var slot in formationSlots)
                {
                    if (slot.unit == null) continue;
                    Vector3 targetPos = newCenter + slot.offset;
                    slot.unit.MoveTo(targetPos);
                    // slot.unit.transform.rotation = slot.rotation; // Removed to allow NavMeshAgent to handle rotation
                }
            }
            else
            {
                int unitCount = selectedUnits.Count;
                if (unitCount == 0) return;
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

    IEnumerator RebuildCurrentFormation()
    {
        if (isSwitchingFormation) yield break;
        
        isSwitchingFormation = true;

        FormationMode modeToRebuild = currentMode;
        
        // Rebuilding shouldn't have a break animation, so we just clean the state.
        CleanupPreviousFormation();
        currentMode = modeToRebuild;

        List<Coroutine> formationCoroutines = new List<Coroutine>();
        switch (currentMode)
        {
            case FormationMode.Mode2D:
                if (selectedUnits.Count > 0) formationCoroutines = UpdateFormation();
                break;
            case FormationMode.Mode3D:
                if (selectedUnits.Count > 1) formationCoroutines = Update3DFormation();
                break;
            case FormationMode.LadderMode:
                if (selectedUnits.Count > 0) formationCoroutines = UpdateLadderFormation();
                break;
        }

        if (formationCoroutines != null)
        {
            foreach (var co in formationCoroutines)
            {
                if (co != null) yield return co;
            }
        }
        
        isSwitchingFormation = false;
    }

    void HandleSingleSelection()
    {
        var preSelection = new HashSet<Unit>(selectedUnits);

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            Unit unit = hit.collider.GetComponent<Unit>();
            if (unit != null)
            {
                if (!Keyboard.current.shiftKey.isPressed && !Keyboard.current.leftCtrlKey.isPressed)
                {
                    ClearSelection();
                }
                
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

        var postSelection = new HashSet<Unit>(selectedUnits);
        if (!preSelection.SetEquals(postSelection) && currentMode != FormationMode.None)
        {
            StartCoroutine(RebuildCurrentFormation());
        }
    }

    void HandleDragSelection()
    {
        var preSelection = new HashSet<Unit>(selectedUnits);

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
        
        var postSelection = new HashSet<Unit>(selectedUnits);
        if (!preSelection.SetEquals(postSelection) && currentMode != FormationMode.None)
        {
            StartCoroutine(RebuildCurrentFormation());
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
            if (currentFormationGroup != null)
            {
                currentFormationGroup.units.Remove(unit);
            }
        }
    }

    void ClearSelection()
    {
        if (selectedUnits.Count == 0 && currentMode == FormationMode.None) return;
        
        foreach (Unit unit in selectedUnits)
        {
            unit.ToggleSelection(false);
        }
        selectedUnits.Clear();
        StartCoroutine(BreakFormation(true, FormationMode.None));
    }

    private void TeleportAndResetUnits(List<Unit> unitsToReset, Vector3 center)
    {
        for (int i = 0; i < unitsToReset.Count; i++)
        {
            Unit unit = unitsToReset[i];
            if (unit == null) continue;
            float angleRad = Mathf.Deg2Rad * (360f / unitsToReset.Count * i);
            Vector3 scatterTarget = center + new Vector3(Mathf.Cos(angleRad), 0, Mathf.Sin(angleRad)) * 1.5f;
            
            unit.StopAllCoroutines();
            unit.transform.position = scatterTarget;
            unit.EnableNavMeshAgent(true);
            unit.SetNavMeshAgentControl(true);
            unit.UnlockRotation();
            unit.ClearLeader();
            unit.IsLeader = false;
            unit.formationGroup = null;
        }
    }

    IEnumerator BreakFormation(bool animate, FormationMode nextMode = FormationMode.None)
    {
        FormationMode modeToBreak = currentMode;
        currentMode = FormationMode.None;

        if (modeToBreak == FormationMode.Mode3D || modeToBreak == FormationMode.LadderMode)
        {
            if (currentFormationParent != null)
            {
                List<Unit> unitsToBreak = new List<Unit>();
                foreach (Transform child in currentFormationParent.transform)
                {
                    Unit unit = child.GetComponent<Unit>();
                    if (unit != null) unitsToBreak.Add(unit);
                }

                foreach (var unit in unitsToBreak) unit.transform.parent = null;
                Destroy(currentFormationParent);
                currentFormationParent = null;

                Vector3 center = Vector3.zero;
                if (unitsToBreak.Count > 0)
                {
                    foreach(var unit in unitsToBreak) center += unit.transform.position;
                    center /= unitsToBreak.Count;
                }
                center.y = 0.0f; // Calculate center based on ground plane for consistency

                // Skip animation if the next mode is LadderMode
                bool useAnimation = animate && nextMode != FormationMode.LadderMode;

                if (useAnimation)
                {
                    List<Coroutine> runningBreaks = new List<Coroutine>();
                    
                    if (modeToBreak == FormationMode.LadderMode && nextMode == FormationMode.Mode3D)
                    {
                        List<Vector3> futureOffsets = Get3DFormationOffsets(unitsToBreak.Count);
                        for (int i = 0; i < unitsToBreak.Count; i++)
                        {
                            Unit unit = unitsToBreak[i];
                            if (unit == null) continue;

                            Vector3 finalOffset = futureOffsets[i];
                            if (Mathf.Approximately(finalOffset.y, 0))
                            {
                                finalOffset.y += CUBE_SIZE / 2.0f;
                            }
                            
                            Vector3 scatterTarget = center + finalOffset;
                            runningBreaks.Add(StartCoroutine(unit.FallAndScatter(scatterTarget, 4f)));
                        }
                    }
                    else if (modeToBreak == FormationMode.LadderMode && nextMode == FormationMode.Mode2D)
                    {
                        int unitCount = unitsToBreak.Count;
                        if (unitCount > 0)
                        {
                            const float baseSideLength = 3.0f;
                            float desiredSideLength = baseSideLength + (unitCount * 0.2f);
                            float radius = desiredSideLength / (2 * Mathf.Sin(Mathf.PI / unitCount));
                            float angleIncrement = 360f / unitCount;

                            for (int i = 0; i < unitCount; i++)
                            {
                                Unit unit = unitsToBreak[i];
                                if (unit == null) continue;

                                float angleRad = Mathf.Deg2Rad * (angleIncrement * i);
                                float x = Mathf.Cos(angleRad) * radius;
                                float z = Mathf.Sin(angleRad) * radius;
                                Vector3 scatterTarget = center + new Vector3(x, CUBE_SIZE / 2.0f, z);
                                
                                runningBreaks.Add(StartCoroutine(unit.FallAndScatter(scatterTarget, 4f)));
                            }
                        }
                    }
                    else // Default circular scatter
                    {
                        for (int i = 0; i < unitsToBreak.Count; i++)
                        {
                            Unit unit = unitsToBreak[i];
                            if (unit == null) continue;
                            float angleRad = Mathf.Deg2Rad * (360f / unitsToBreak.Count * i);
                            Vector3 scatterTarget = center + new Vector3(Mathf.Cos(angleRad), CUBE_SIZE / 2.0f, Mathf.Sin(angleRad)) * 1.5f;
                            runningBreaks.Add(StartCoroutine(unit.FallAndScatter(scatterTarget, 4f)));
                        }
                    }

                    foreach (var coroutine in runningBreaks)
                    {
                        yield return coroutine;
                    }
                }
                else
                {
                    TeleportAndResetUnits(unitsToBreak, center);
                }
            }
        }
        else if (modeToBreak == FormationMode.Mode2D)
        {
            if (currentFormationGroup != null)
            {
                foreach (var unit in currentFormationGroup.units)
                {
                    if (unit != null)
                    {
                        unit.UnlockRotation();
                        unit.formationGroup = null;
                    }
                }
            }
        }
        
        if (currentFormationGroup != null)
        {
            currentFormationGroup.units.Clear();
            currentFormationGroup = null;
        }
        formationSlots.Clear();
        if (lineDrawer != null) lineDrawer.ClearLines();
    }
}