using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 1.5f, -6f);
    public float smoothSpeed = 5f;

    void Start()
    {
        // Auto-detect target if not assigned
        if (target == null)
        {
            // Try to find ThirdPersonController first
            ThirdPersonController controller = FindObjectOfType<ThirdPersonController>();
            if (controller != null)
            {
                target = controller.transform;
            }
            else
            {
                target = GameObject.Find("Armature")?.transform;
            }
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Follow on X axis (side scroller style)
        Vector3 desiredPos = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            offset.z
        );

        transform.position = Vector3.Lerp(
            transform.position, desiredPos, smoothSpeed * Time.deltaTime
        );
        
        // Debug - uncomment to see what's happening
        // Debug.Log($"Target pos: {target.position}, Camera pos: {transform.position}");
    }
}