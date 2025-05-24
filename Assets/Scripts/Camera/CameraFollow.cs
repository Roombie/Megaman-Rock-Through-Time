using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform player;
    public float smoothDampTime = 0.1f;
    public Vector3 lookAhead;

    public Vector2 levelBoundsMin;
    public Vector2 levelBoundsMax;

    public bool lockHorizontal = false;
    public bool lockVertical = false;

    private float camHalfWidth;
    private float camHalfHeight;

    private Camera cam;

    private Vector3 velocity = Vector3.zero;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (player == null) return;

        camHalfHeight = cam.orthographicSize;
        camHalfWidth = camHalfHeight * cam.aspect;

        Vector3 targetPos = player.position + lookAhead;
        targetPos.z = transform.position.z;

        float minX = levelBoundsMin.x + camHalfWidth;
        float maxX = levelBoundsMax.x - camHalfWidth;
        float minY = levelBoundsMin.y + camHalfHeight;
        float maxY = levelBoundsMax.y - camHalfHeight;

        if (!lockHorizontal)
        {
            if (levelBoundsMax.x - levelBoundsMin.x < camHalfWidth * 2)
                targetPos.x = (levelBoundsMin.x + levelBoundsMax.x) / 2f;
            else
                targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
        }
        else
        {
            targetPos.x = transform.position.x;
        }

        if (!lockVertical)
        {
            if (levelBoundsMax.y - levelBoundsMin.y < camHalfHeight * 2)
                targetPos.y = (levelBoundsMin.y + levelBoundsMax.y) / 2f;
            else
                targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);
        }
        else
        {
            targetPos.y = transform.position.y;
        }

        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothDampTime);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 bottomLeft = new Vector3(levelBoundsMin.x, levelBoundsMin.y, transform.position.z);
        Vector3 bottomRight = new Vector3(levelBoundsMax.x, levelBoundsMin.y, transform.position.z);
        Vector3 topRight = new Vector3(levelBoundsMax.x, levelBoundsMax.y, transform.position.z);
        Vector3 topLeft = new Vector3(levelBoundsMin.x, levelBoundsMax.y, transform.position.z);

        Gizmos.DrawLine(bottomLeft, bottomRight);
        Gizmos.DrawLine(bottomRight, topRight);
        Gizmos.DrawLine(topRight, topLeft);
        Gizmos.DrawLine(topLeft, bottomLeft);
    }
}