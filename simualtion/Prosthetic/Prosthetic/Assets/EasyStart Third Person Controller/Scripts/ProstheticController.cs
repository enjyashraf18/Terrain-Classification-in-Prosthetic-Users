using UnityEngine;
using UnityEngine.Animations.Rigging;

public class ProstheticController : MonoBehaviour
{
    [Header("Leg IK References")]
    public TwoBoneIKConstraint leftLegIK;
    public TwoBoneIKConstraint rightLegIK;

    [Header("Gait Settings")]
    public float baseStepDistance = 0.4f;
    public float baseStepHeight = 0.15f;
    public float baseStepSpeed = 4f;

    [Header("Terrain Detection (Raycast)")]
    public LayerMask groundLayer;          
    public float raycastDistance = 3f;     
    public float raycastOriginHeight = 1f; 

    [Header("Ground Reference")]
    public GroundMaterialManager groundManager;

    [Header("Stiffness Settings")]
    public float currentStiffness = 1f;
    private float targetStiffness = 1f;
    private float stiffnessChangeSpeed = 0.3f;

    // Foot targets
    private Transform leftFootTarget;
    private Transform rightFootTarget;

    // Foot step tracking
    private Vector3 leftFootPos;
    private Vector3 rightFootPos;
    private Vector3 leftFootNextPos;
    private Vector3 rightFootNextPos;

    private float leftStepProgress = 1f;
    private float rightStepProgress = 1f;

    private bool leftStepping = false;
    private bool rightStepping = false;

    void Start()
    {
        TwoBoneIKConstraint[] allIKs = GetComponentsInChildren<TwoBoneIKConstraint>(true);

        foreach (TwoBoneIKConstraint ik in allIKs)
        {
            string name = ik.gameObject.name.ToLower();
            if (name == "left leg") leftLegIK = ik;
            else if (name == "right leg") rightLegIK = ik;
        }

        if (leftLegIK != null) leftFootTarget = leftLegIK.data.target;
        if (rightLegIK != null) rightFootTarget = rightLegIK.data.target;

        if (leftFootTarget != null)
        {
            leftFootPos = leftFootTarget.position;
            leftFootNextPos = leftFootPos;
        }
        if (rightFootTarget != null)
        {
            rightFootPos = rightFootTarget.position;
            rightFootNextPos = rightFootPos;
        }

        if (groundManager == null)
            groundManager = FindObjectOfType<GroundMaterialManager>();

        Debug.Log("Prosthetic Controller initialized");
    }

    void Update()
    {
        DetectCurrentMaterial();
        UpdateStiffness();
        UpdateGait();
    }

    void UpdateStiffness()
    {
        currentStiffness = Mathf.Lerp(currentStiffness, targetStiffness,
                                      Time.deltaTime * stiffnessChangeSpeed);
    }

    void UpdateGait()
    {
        if (leftFootTarget == null || rightFootTarget == null) return;

        // ===== SAME DRAMATIC GAIT PARAMS AS OLD VERSION =====
        float effectiveStepSpeed = baseStepSpeed * (0.2f + currentStiffness * 2.8f);
        float effectiveStepHeight = baseStepHeight * (0.1f + (1f - currentStiffness) * 4f);
        float effectiveStepDistance = baseStepDistance * (0.3f + currentStiffness * 1.7f);

        Vector3 bodyPos = transform.position;
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        Vector3 leftIdeal = bodyPos + forward * effectiveStepDistance * 0.5f - right * 0.15f;
        Vector3 rightIdeal = bodyPos + forward * effectiveStepDistance * 0.5f + right * 0.15f;

        // Snap to the actual surface (filtered by groundLayer again)
        leftIdeal = SnapToGround(leftIdeal);
        rightIdeal = SnapToGround(rightIdeal);

        float leftDist = Vector3.Distance(leftFootPos, leftIdeal);
        float rightDist = Vector3.Distance(rightFootPos, rightIdeal);

        if (!leftStepping && !rightStepping)
        {
            if (leftDist > effectiveStepDistance)
            {
                leftStepping = true;
                leftStepProgress = 0f;
                leftFootNextPos = leftIdeal;
            }
            else if (rightDist > effectiveStepDistance)
            {
                rightStepping = true;
                rightStepProgress = 0f;
                rightFootNextPos = rightIdeal;
            }
        }
        else if (leftStepping && !rightStepping)
        {
            if (rightDist > effectiveStepDistance * 1.5f)
            {
                rightStepping = true;
                rightStepProgress = 0f;
                rightFootNextPos = rightIdeal;
            }
        }

        if (leftStepping)
        {
            leftStepProgress += Time.deltaTime * effectiveStepSpeed;
            float t = Mathf.Clamp01(leftStepProgress);

            Vector3 flatPos = Vector3.Lerp(leftFootPos, leftFootNextPos, t);
            float arc = Mathf.Sin(t * Mathf.PI) * effectiveStepHeight;
            leftFootTarget.position = flatPos + Vector3.up * arc;

            if (leftStepProgress >= 1f)
            {
                leftFootPos = leftFootNextPos;
                leftStepping = false;
            }
        }
        else
        {
            leftFootTarget.position = leftFootPos;
        }

        if (rightStepping)
        {
            rightStepProgress += Time.deltaTime * effectiveStepSpeed;
            float t = Mathf.Clamp01(rightStepProgress);

            Vector3 flatPos = Vector3.Lerp(rightFootPos, rightFootNextPos, t);
            float arc = Mathf.Sin(t * Mathf.PI) * effectiveStepHeight;
            rightFootTarget.position = flatPos + Vector3.up * arc;

            if (rightStepProgress >= 1f)
            {
                rightFootPos = rightFootNextPos;
                rightStepping = false;
            }
        }
        else
        {
            rightFootTarget.position = rightFootPos;
        }
    }

    Vector3 SnapToGround(Vector3 pos)
    {
        RaycastHit hit;
        Vector3 origin = pos + Vector3.up * raycastOriginHeight;

        if (Physics.Raycast(origin, Vector3.down, out hit, raycastDistance, groundLayer))
            return hit.point;

        return pos;
    }

    void DetectCurrentMaterial()
    {
        if (groundManager == null) return;

        string materialType = groundManager.GetCurrentMaterial();
        UpdateTerrainStiffness(materialType);
    }

    void UpdateTerrainStiffness(string materialType)
    {
        switch (materialType)
        {
            case "Concrete":
                targetStiffness = 1.0f;
                break;
            case "Sand":
                targetStiffness = 0.2f;
                break;
            case "Grass":
                targetStiffness = 0.6f;
                break;
            case "Stair":
                DetectStairDirection(); // keeps your stair up/down logic
                break;
            default:
                targetStiffness = 1.0f;
                break;
        }
    }

    void DetectStairDirection()
    {
        RaycastHit hitCurrent;
        Vector3 currentCheckPos = transform.position + Vector3.up * 0.3f;

        if (!Physics.Raycast(currentCheckPos, Vector3.down, out hitCurrent, 2f, groundLayer))
        {
            targetStiffness = 0.8f;
            return;
        }

        Vector3 forward = transform.forward;
        Vector3 frontPos = transform.position + forward * 1f + Vector3.up * 0.3f;

        RaycastHit hitFront;
        if (!Physics.Raycast(frontPos, Vector3.down, out hitFront, 2f, groundLayer))
        {
            targetStiffness = 0.8f;
            return;
        }

        float heightDiff = hitFront.point.y - hitCurrent.point.y;

        if (heightDiff > 0.1f) targetStiffness = 0.9f;      // up
        else if (heightDiff < -0.1f) targetStiffness = 0.7f; // down
        else targetStiffness = 0.8f;
    }
}





// using UnityEngine;
// using UnityEngine.Animations.Rigging;

// public class ProstheticController : MonoBehaviour
// {
//     [Header("Leg IK References")]
//     public TwoBoneIKConstraint leftLegIK;
//     public TwoBoneIKConstraint rightLegIK;

//     [Header("Gait Settings")]
//     public float baseStepDistance = 0.4f;
//     public float baseStepHeight = 0.15f;
//     public float baseStepSpeed = 4f;

//     [Header("Terrain Detection")]
//     public LayerMask groundLayer;
//     public float raycastDistance = 2f;

//     [Header("Stiffness Settings")]
//     public float currentStiffness = 1f;
//     private float targetStiffness = 1f;
//     private float stiffnessChangeSpeed = 0.3f;  // Very slow change for dramatic effect

//     // Foot targets
//     private Transform leftFootTarget;
//     private Transform rightFootTarget;

//     // Foot step tracking
//     private Vector3 leftFootPos;
//     private Vector3 rightFootPos;
//     private Vector3 leftFootNextPos;
//     private Vector3 rightFootNextPos;

//     private float leftStepProgress = 1f;
//     private float rightStepProgress = 1f;

//     private bool leftStepping = false;
//     private bool rightStepping = false;

//     void Start()
//     {
//         TwoBoneIKConstraint[] allIKs =
//             GetComponentsInChildren<TwoBoneIKConstraint>(true);

//         foreach (TwoBoneIKConstraint ik in allIKs)
//         {
//             string name = ik.gameObject.name.ToLower();
//             if (name == "left leg")  leftLegIK  = ik;
//             else if (name == "right leg") rightLegIK = ik;
//         }

//         if (leftLegIK != null)
//             leftFootTarget = leftLegIK.data.target;
//         if (rightLegIK != null)
//             rightFootTarget = rightLegIK.data.target;

//         if (leftFootTarget != null)
//         {
//             leftFootPos = leftFootTarget.position;
//             leftFootNextPos = leftFootPos;
//         }
//         if (rightFootTarget != null)
//         {
//             rightFootPos = rightFootTarget.position;
//             rightFootNextPos = rightFootPos;
//         }

//         Debug.Log("Left leg found: " + leftLegIK);
//         Debug.Log("Right leg found: " + rightLegIK);
//     }

//     void Update()
//     {
//         DetectTerrain();
//         UpdateStiffness();
//         UpdateGait();
//     }

//     void UpdateStiffness()
//     {
//         // Smoothly transition to target stiffness (very slow for drama!)
//         currentStiffness = Mathf.Lerp(currentStiffness, targetStiffness, 
//                                       Time.deltaTime * stiffnessChangeSpeed);
        
//         Debug.Log($"Current Stiffness: {currentStiffness:F2} | Target: {targetStiffness:F2}");
//     }

//     void UpdateGait()
//     {
//         if (leftFootTarget == null || rightFootTarget == null) return;

//         // ===== DRAMATIC STIFFNESS EFFECTS =====
        
//         // Step Speed: Stiff = 3x FASTER, Soft = 3x SLOWER
//         float effectiveStepSpeed = baseStepSpeed * (0.2f + currentStiffness * 2.8f);
        
//         // Step Height: Stiff = almost no arc, Soft = HUGE bouncy arc
//         float effectiveStepHeight = baseStepHeight * (0.1f + (1f - currentStiffness) * 4f);
        
//         // Step Distance: Stiff = short quick steps, Soft = long slow strides
//         float effectiveStepDistance = baseStepDistance * (0.3f + currentStiffness * 1.7f);

//         // --- Decide when to take a new step ---
//         Vector3 bodyPos = transform.position;
//         Vector3 forward = transform.forward;
//         Vector3 right   = transform.right;

//         Vector3 leftIdeal  = bodyPos + forward * effectiveStepDistance * 0.5f 
//                            - right * 0.15f;
//         Vector3 rightIdeal = bodyPos + forward * effectiveStepDistance * 0.5f 
//                            + right * 0.15f;

//         leftIdeal  = SnapToGround(leftIdeal);
//         rightIdeal = SnapToGround(rightIdeal);

//         float leftDist  = Vector3.Distance(leftFootPos,  leftIdeal);
//         float rightDist = Vector3.Distance(rightFootPos, rightIdeal);

//         if (!leftStepping && !rightStepping)
//         {
//             if (leftDist > effectiveStepDistance)
//             {
//                 leftStepping     = true;
//                 leftStepProgress = 0f;
//                 leftFootNextPos  = leftIdeal;
//             }
//             else if (rightDist > effectiveStepDistance)
//             {
//                 rightStepping     = true;
//                 rightStepProgress = 0f;
//                 rightFootNextPos  = rightIdeal;
//             }
//         }
//         else if (leftStepping && !rightStepping)
//         {
//             if (rightDist > effectiveStepDistance * 1.5f)
//             {
//                 rightStepping     = true;
//                 rightStepProgress = 0f;
//                 rightFootNextPos  = rightIdeal;
//             }
//         }

//         // --- Animate the stepping foot (swing phase) ---
//         if (leftStepping)
//         {
//             leftStepProgress += Time.deltaTime * effectiveStepSpeed;
//             float t = Mathf.Clamp01(leftStepProgress);

//             Vector3 flatPos = Vector3.Lerp(leftFootPos, leftFootNextPos, t);
//             float arc = Mathf.Sin(t * Mathf.PI) * effectiveStepHeight;
//             leftFootTarget.position = flatPos + Vector3.up * arc;

//             if (leftStepProgress >= 1f)
//             {
//                 leftFootPos   = leftFootNextPos;
//                 leftStepping  = false;
//             }
//         }
//         else
//         {
//             leftFootTarget.position = leftFootPos;
//         }

//         if (rightStepping)
//         {
//             rightStepProgress += Time.deltaTime * effectiveStepSpeed;
//             float t = Mathf.Clamp01(rightStepProgress);

//             Vector3 flatPos = Vector3.Lerp(rightFootPos, rightFootNextPos, t);
//             float arc = Mathf.Sin(t * Mathf.PI) * effectiveStepHeight;
//             rightFootTarget.position = flatPos + Vector3.up * arc;

//             if (rightStepProgress >= 1f)
//             {
//                 rightFootPos  = rightFootNextPos;
//                 rightStepping = false;
//             }
//         }
//         else
//         {
//             rightFootTarget.position = rightFootPos;
//         }
//     }

//     Vector3 SnapToGround(Vector3 pos)
//     {
//         RaycastHit hit;
//         Vector3 origin = pos + Vector3.up * 1f;
//         if (Physics.Raycast(origin, Vector3.down, out hit, 3f, groundLayer))
//             return hit.point;
//         return pos;
//     }

//     void DetectTerrain()
//     {
//         if (leftFootTarget == null) return;

//         RaycastHit hit;
//         if (Physics.Raycast(leftFootTarget.position + Vector3.up * 0.1f,
//                             Vector3.down, out hit, 0.5f))
//         {
//             string tag = hit.collider.gameObject.tag;

//             if (tag == "Stair")
//             {
//                 float footY = leftFootTarget.position.y;
//                 float prevY = transform.position.y;
                
//                 if (footY > prevY - 0.5f)
//                 {
//                     SetTerrainStiffness(0.95f, "⬆️ CLIMBING UP (Super stiff, powerful steps)");
//                 }
//                 else
//                 {
//                     SetTerrainStiffness(0.4f, "⬇️ CLIMBING DOWN (Soft, careful steps)");
//                 }
//             }
//             else
//             {
//                 UpdateTerrainStiffness(tag);
//             }
//         }
//     }

//     void UpdateTerrainStiffness(string terrainTag)
//     {
//         switch (terrainTag)
//         {
//             case "Concrete":
//                 SetTerrainStiffness(1.0f, "🏢 CONCRETE (Rigid, machine-like, fast steps!)");
//                 break;
//             case "Sand":
//                 SetTerrainStiffness(0.2f, "🏜️ SAND (Super bouncy! Huge high steps, slow!)");
//                 break;
//             case "Grass":
//                 SetTerrainStiffness(0.6f, "🌿 GRASS (Balanced bouncy movement)");
//                 break;
//             default:
//                 SetTerrainStiffness(1.0f, "Default");
//                 break;
//         }
//     }

//     void SetTerrainStiffness(float newStiffness, string terrainName)
//     {
//         if (Mathf.Abs(newStiffness - targetStiffness) > 0.05f)
//         {
//             Debug.Log($"\n========== {terrainName} ==========\n");
//         }

//         targetStiffness = newStiffness;
//     }
// }










// using UnityEngine;
// using UnityEngine.Animations.Rigging;

// public class ProstheticController : MonoBehaviour
// {
//     [Header("Leg IK References")]
//     public TwoBoneIKConstraint leftLegIK;
//     public TwoBoneIKConstraint rightLegIK;

//     [Header("Gait Settings")]
//     public float stepDistance = 0.4f;
//     public float stepHeight = 0.15f;
//     public float stepSpeed = 4f;

//     [Header("Terrain Detection")]
//     public LayerMask groundLayer;
//     public float raycastDistance = 2f;

//     [Header("Stiffness Settings")]
//     public float currentStiffness = 1f;
//     private float targetStiffness = 1f;
//     private float stiffnessChangeSpeed = 2f;

//     // Foot targets
//     private Transform leftFootTarget;
//     private Transform rightFootTarget;

//     // Foot step tracking
//     private Vector3 leftFootPos;
//     private Vector3 rightFootPos;
//     private Vector3 leftFootNextPos;
//     private Vector3 rightFootNextPos;

//     private float leftStepProgress = 1f;   // 1 = step complete
//     private float rightStepProgress = 1f;

//     private bool leftStepping = false;
//     private bool rightStepping = false;

//     void Start()
//     {
//         TwoBoneIKConstraint[] allIKs =
//             GetComponentsInChildren<TwoBoneIKConstraint>(true);

//         foreach (TwoBoneIKConstraint ik in allIKs)
//         {
//             string name = ik.gameObject.name.ToLower();
//             if (name == "left leg")  leftLegIK  = ik;
//             else if (name == "right leg") rightLegIK = ik;
//         }

//         // Get the foot target transforms from the IK
//         if (leftLegIK != null)
//             leftFootTarget = leftLegIK.data.target;
//         if (rightLegIK != null)
//             rightFootTarget = rightLegIK.data.target;

//         // Initialize foot positions to where they are now
//         if (leftFootTarget != null)
//         {
//             leftFootPos = leftFootTarget.position;
//             leftFootNextPos = leftFootPos;
//         }
//         if (rightFootTarget != null)
//         {
//             rightFootPos = rightFootTarget.position;
//             rightFootNextPos = rightFootPos;
//         }

//         Debug.Log("Left leg found: " + leftLegIK);
//         Debug.Log("Right leg found: " + rightLegIK);
//     }

//     void Update()
//     {
//         DetectTerrain();
//         UpdateStiffness();
//         UpdateGait();
//     }

//     void UpdateStiffness()
//     {
//         // Smoothly transition to target stiffness
//         currentStiffness = Mathf.Lerp(currentStiffness, targetStiffness, 
//                                       Time.deltaTime * stiffnessChangeSpeed);
//     }

//     void UpdateGait()
//     {
//         if (leftFootTarget == null || rightFootTarget == null) return;

//         // Calculate effective step parameters based on stiffness
//         float effectiveStepSpeed = stepSpeed * currentStiffness;
//         float effectiveStepHeight = stepHeight * (1f - currentStiffness * 0.3f); // Stiffer = lower step
//         float effectiveStepDistance = stepDistance * (2f - currentStiffness); // Stiffer = shorter steps

//         // --- Decide when to take a new step ---

//         // Ideal foot positions = slightly in front and to the side of the body
//         Vector3 bodyPos = transform.position;
//         Vector3 forward = transform.forward;
//         Vector3 right   = transform.right;

//         Vector3 leftIdeal  = bodyPos + forward * effectiveStepDistance * 0.5f 
//                            - right * 0.15f;
//         Vector3 rightIdeal = bodyPos + forward * effectiveStepDistance * 0.5f 
//                            + right * 0.15f;

//         // Snap ideal positions to the ground using Raycast
//         leftIdeal  = SnapToGround(leftIdeal);
//         rightIdeal = SnapToGround(rightIdeal);

//         // Check if foot is too far from ideal position → time to step
//         float leftDist  = Vector3.Distance(leftFootPos,  leftIdeal);
//         float rightDist = Vector3.Distance(rightFootPos, rightIdeal);

//         // Alternate legs — only one foot lifts at a time
//         if (!leftStepping && !rightStepping)
//         {
//             if (leftDist > effectiveStepDistance)
//             {
//                 leftStepping     = true;
//                 leftStepProgress = 0f;
//                 leftFootNextPos  = leftIdeal;
//             }
//             else if (rightDist > effectiveStepDistance)
//             {
//                 rightStepping     = true;
//                 rightStepProgress = 0f;
//                 rightFootNextPos  = rightIdeal;
//             }
//         }
//         else if (leftStepping && !rightStepping)
//         {
//             if (rightDist > effectiveStepDistance * 1.5f)
//             {
//                 rightStepping     = true;
//                 rightStepProgress = 0f;
//                 rightFootNextPos  = rightIdeal;
//             }
//         }

//         // --- Animate the stepping foot (swing phase) ---
//         if (leftStepping)
//         {
//             leftStepProgress += Time.deltaTime * effectiveStepSpeed;
//             float t = Mathf.Clamp01(leftStepProgress);

//             // Move foot toward next position + arc upward (swing phase)
//             Vector3 flatPos = Vector3.Lerp(leftFootPos, leftFootNextPos, t);
//             float arc = Mathf.Sin(t * Mathf.PI) * effectiveStepHeight;
//             leftFootTarget.position = flatPos + Vector3.up * arc;

//             if (leftStepProgress >= 1f)
//             {
//                 leftFootPos   = leftFootNextPos;
//                 leftStepping  = false;
//             }
//         }
//         else
//         {
//             // Stance phase — foot stays planted
//             leftFootTarget.position = leftFootPos;
//         }

//         if (rightStepping)
//         {
//             rightStepProgress += Time.deltaTime * effectiveStepSpeed;
//             float t = Mathf.Clamp01(rightStepProgress);

//             Vector3 flatPos = Vector3.Lerp(rightFootPos, rightFootNextPos, t);
//             float arc = Mathf.Sin(t * Mathf.PI) * effectiveStepHeight;
//             rightFootTarget.position = flatPos + Vector3.up * arc;

//             if (rightStepProgress >= 1f)
//             {
//                 rightFootPos  = rightFootNextPos;
//                 rightStepping = false;
//             }
//         }
//         else
//         {
//             rightFootTarget.position = rightFootPos;
//         }
//     }

//     Vector3 SnapToGround(Vector3 pos)
//     {
//         RaycastHit hit;
//         Vector3 origin = pos + Vector3.up * 1f;
//         if (Physics.Raycast(origin, Vector3.down, out hit, 3f, groundLayer))
//             return hit.point;
//         return pos;
//     }

//     void DetectTerrain()
//     {
//         if (leftFootTarget == null) return;

//         RaycastHit hit;
//         if (Physics.Raycast(leftFootTarget.position + Vector3.up * 0.1f,
//                             Vector3.down, out hit, 0.5f))
//         {
//             string tag = hit.collider.gameObject.tag;

//             if (tag == "Stair")
//             {
//                 float footY = leftFootTarget.position.y;
//                 float prevY = transform.position.y;
                
//                 if (footY > prevY - 0.5f)
//                 {
//                     targetStiffness = 0.9f;
//                     Debug.Log("Climbing UP | Stiffness: 0.9");
//                 }
//                 else
//                 {
//                     targetStiffness = 0.7f;
//                     Debug.Log("Climbing DOWN | Stiffness: 0.7");
//                 }
//             }
//             else
//             {
//                 UpdateTerrainStiffness(tag);
//             }
//         }
//     }

//     void UpdateTerrainStiffness(string terrainTag)
//     {
//         switch (terrainTag)
//         {
//             case "Concrete":
//                 targetStiffness = 1.0f;   // firm, full stiffness
//                 Debug.Log("Terrain: Concrete | Stiffness: 1.0");
//                 break;
//             case "Sand":
//                 targetStiffness = 0.5f;   // soft, absorb movement
//                 Debug.Log("Terrain: Sand | Stiffness: 0.5");
//                 break;
//             case "Grass":
//                 targetStiffness = 0.75f;  // medium
//                 Debug.Log("Terrain: Grass | Stiffness: 0.75");
//                 break;
//             default:
//                 targetStiffness = 1.0f;
//                 break;
//         }
//     }
// }