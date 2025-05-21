using UnityEngine;
using UnityEngine.U2D;

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
    private PixelPerfectCamera pixelPerfect;

    void Awake()
    {
        cam = GetComponent<Camera>();
        pixelPerfect = GetComponent<PixelPerfectCamera>();
    }

    void LateUpdate()
    {
        if (player == null) return;

        float orthoSize = cam.orthographicSize;
        float aspect = cam.aspect;

        camHalfHeight = orthoSize;
        camHalfWidth = orthoSize * aspect;

        // Ajustar los límites si el modo es WidedExpand
        if (ScreenDisplayManager.Instance != null &&
            PlayerPrefs.GetInt(SettingsKeys.ScreenKey, 0) == (int)ScreenDisplayMode.WidedExpand)
        {
            float baseWidth = 240f / 2f / 16f; // Altura ortográfica por defecto
            float baseAspect = 4f / 3f;
            float baseHalfWidth = baseWidth * baseAspect;
            float extraSpace = camHalfWidth - baseHalfWidth;

            levelBoundsMin.x -= extraSpace;
            levelBoundsMax.x += extraSpace;
        }

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

        float t = 1f - Mathf.Pow(1f - smoothDampTime, Time.deltaTime * 30);
        transform.position = Vector3.Lerp(transform.position, targetPos, t);
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