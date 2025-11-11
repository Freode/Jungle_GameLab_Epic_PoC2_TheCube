using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using System.Linq;

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
    public FormationGroup formationGroup; // Reference to the FormationGroup component in the scene

    public List<Unit> units = new List<Unit>();
    public List<Unit> selectedUnits = new List<Unit>();

    private Vector2 startDrag;
    private Vector2 endDrag;
    private bool isDragging;
    private Texture2D selectionBoxTexture;

    private FormationMode currentMode = FormationMode.Individual;
    private List<FormationSlot> formationSlots = new List<FormationSlot>();
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

        // At the start, all units are in the group for individual fall checks
        if (formationGroup != null)
        {
            formationGroup.units.AddRange(units);
            formationGroup.currentMode = FormationMode.Individual;
        }

        foreach (var unit in units)
        {
            unit.currentMode = FormationMode.Individual;
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

        // Formation Mode Switching
        if (Keyboard.current.digit0Key.wasPressedThisFrame)
        {
            StartCoroutine(SwitchFormation(FormationMode.Individual));
        }
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (selectedUnits.Count > 0)
            {
                StartCoroutine(SwitchFormation(FormationMode.Cluster));
            }
        }
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            if (selectedUnits.Count > 1)
            {
                StartCoroutine(SwitchFormation(FormationMode.Solid));
            }
        }
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            if (selectedUnits.Count > 0)
            {
                StartCoroutine(SwitchFormation(FormationMode.Ladder));
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
            case FormationMode.Cluster:
                StartCoroutine(ExecuteMode2DSkill());
                break;
            case FormationMode.Solid:
                Debug.Log("Solid Mode Skill Activated (Not Implemented)");
                break;
            case FormationMode.Ladder:
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

        // Immediately deselect the unit to remove it from the cluster
        DeselectUnit(topCube);

        // Skill execution is complete, player can now move other units.
        isExecutingSkill = false; 

        // 3. Start the new animation coroutine and wait for it
        yield return StartCoroutine(topCube.MoveForwardAndFall(topCubeTargetWorldPos, 15f, 20f));
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

        // Wait for one frame to ensure agents have stopped
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

        // 2. Dynamic values
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
                slot.unit.MoveTo(center, gatherSpeed);
            }
        }

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

        foreach (var co in spreadCoroutines)
        {
            if (co != null) yield return co;
        }

        isExecutingSkill = false;
    }


    IEnumerator SwitchFormation(FormationMode newMode)
    {
        if (isSwitchingFormation) yield break;
        if (newMode != FormationMode.Individual && currentMode == newMode) yield break;

        isSwitchingFormation = true;

        FormationMode previousMode = currentMode;
        CleanupPreviousFormation(previousMode);

        currentMode = newMode;
        foreach (var unit in units)
        {
            unit.currentMode = newMode;
        }

        if (formationGroup == null)
        {
            Debug.LogError("FormationGroup reference is not assigned in PlayerController. Please assign the FormationManager GameObject to the 'Formation Group' field in the Inspector.", this);
            isSwitchingFormation = false;
            yield break;
        }
        
        formationGroup.currentMode = newMode;
        formationGroup.units.Clear();
        if (newMode == FormationMode.Individual)
        {
            formationGroup.units.AddRange(this.units);
        }
        else
        {
            formationGroup.units.AddRange(selectedUnits);
        }

        if (newMode == FormationMode.Individual)
        {
            isSwitchingFormation = false;
            yield break;
        }

        List<Coroutine> formationCoroutines = new List<Coroutine>();
        switch (newMode)
        {
            case FormationMode.Cluster:
                formationCoroutines = UpdateFormation(previousMode);
                break;
            case FormationMode.Solid:
                formationCoroutines = Update3DFormation(previousMode);
                break;
            case FormationMode.Ladder:
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

    void CleanupPreviousFormation(FormationMode modeToBreak)
    {
        if (modeToBreak == FormationMode.Solid || modeToBreak == FormationMode.Ladder)
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
                }
                
                Destroy(currentFormationParent);
                currentFormationParent = null;
            }
        }
        
        formationSlots.Clear();
        if (lineDrawer != null) lineDrawer.ClearLines();
    }

    List<Coroutine> UpdateFormation(FormationMode previousMode)
    {
        if (selectedUnits.Count < 1) return new List<Coroutine>();

        var coroutines = new List<Coroutine>();
        
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

        bool useFallAnimation = (previousMode == FormationMode.Solid || previousMode == FormationMode.Ladder);

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

    List<Coroutine> Update3DFormation(FormationMode previousMode)
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
            unit.ClearLeader(); // Clear any previous leader to prevent movement conflicts.
            unit.EnableNavMeshAgent(false);
            unit.transform.parent = currentFormationParent.transform;
            
            coroutines.Add(StartCoroutine(MoveToLocalPosition(unit.transform, offset, 0.5f)));

            unit.LockRotation();
            unit.transform.localRotation = Quaternion.identity;
        }
        return coroutines;
    }

    List<Coroutine> UpdateLadderFormation(FormationMode previousMode)
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
        Debug.Log($"[Debug] Move command received. Selected Units: {selectedUnits.Count}, Current Mode: {currentMode}");
        if (selectedUnits.Count == 0) return;
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            if (currentMode == FormationMode.Solid || currentMode == FormationMode.Ladder)
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
            else if (currentMode == FormationMode.Cluster)
            {
                Vector3 newCenter = hit.point;
                foreach (var slot in formationSlots)
                {
                    if (slot.unit == null) continue;
                    Vector3 targetPos = newCenter + slot.offset;
                    slot.unit.MoveTo(targetPos);
                }
            }
            else // Individual mode
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

        // If the selection is now empty, don't rebuild. Instead, break the formation.
        if (selectedUnits.Count == 0)
        {
            StartCoroutine(SwitchFormation(FormationMode.Individual));
            yield break;
        }
        
        isSwitchingFormation = true;

        FormationMode modeToRebuild = currentMode;
        
        CleanupPreviousFormation(modeToRebuild);
        currentMode = modeToRebuild;
        if (formationGroup != null)
        {
            formationGroup.currentMode = modeToRebuild;
            formationGroup.units.Clear();
            formationGroup.units.AddRange(selectedUnits);
        }


        List<Coroutine> formationCoroutines = new List<Coroutine>();
        switch (currentMode)
        {
            case FormationMode.Cluster:
                if (selectedUnits.Count > 0) formationCoroutines = UpdateFormation(FormationMode.Individual);
                break;
            case FormationMode.Solid:
                if (selectedUnits.Count > 1) formationCoroutines = Update3DFormation(FormationMode.Individual);
                break;
            case FormationMode.Ladder:
                if (selectedUnits.Count > 0) formationCoroutines = UpdateLadderFormation(FormationMode.Individual);
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
        if (!preSelection.SetEquals(postSelection) && currentMode != FormationMode.Individual)
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
        if (!preSelection.SetEquals(postSelection) && currentMode != FormationMode.Individual)
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
            if (formationGroup != null)
            {
                formationGroup.units.Remove(unit);
            }
        }
    }

    void ClearSelection()
    {
        if (selectedUnits.Count == 0 && currentMode == FormationMode.Individual) return;
        
        foreach (Unit unit in selectedUnits)
        {
            unit.ToggleSelection(false);
        }
        selectedUnits.Clear();
        StartCoroutine(SwitchFormation(FormationMode.Individual));
    }
}