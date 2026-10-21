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

    private Vector3 velocity = Vector3.zero;

    [Tooltip("Snap the rendered camera position to whole game pixels (1 / PPU). Keeps the world and the HUD from wobbling by a pixel.")]
    public bool snapToPixelGrid = true;
    private PixelPerfectCamera pixelPerfectCamera;
    private Vector3 smoothPosition;    // un-snapped follow position; the transform gets the snapped one
    private bool hasSmoothPosition;

    void Awake()
    {
        cam = GetComponent<Camera>();
        pixelPerfectCamera = GetComponent<PixelPerfectCamera>();
    }

    // Moved from outside (respawn, room transition...): continue from there
    public void SnapTo(Vector3 position)
    {
        smoothPosition = position;
        hasSmoothPosition = true;
        velocity = Vector3.zero;
        transform.position = Snap(position);
    }

    private Vector3 Snap(Vector3 position)
    {
        if (!snapToPixelGrid) return position;
        float ppu = pixelPerfectCamera != null ? pixelPerfectCamera.assetsPPU : 16f;
        // Multiples of 1/16 are exact floats, so the Screen Space Camera HUD lands on the same pixels every frame
        return new Vector3(Mathf.Round(position.x * ppu) / ppu, Mathf.Round(position.y * ppu) / ppu, position.z);
    }

    void LateUpdate()
    {
        // No target set (or it was destroyed: a respawn): follow player 1
        if (player == null && Players.First != null) player = Players.First.transform;
        if (player == null) return;

        // Someone else moved the camera (it is no longer where we left it): follow from there
        if (!hasSmoothPosition || (transform.position - Snap(smoothPosition)).sqrMagnitude > 0.0001f)
        {
            smoothPosition = transform.position;
            hasSmoothPosition = true;
        }

        camHalfHeight = cam.orthographicSize;
        camHalfWidth = camHalfHeight * cam.aspect;

        Vector3 targetPos = player.position + lookAhead;
        targetPos.z = smoothPosition.z;

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
            targetPos.x = smoothPosition.x;
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
            targetPos.y = smoothPosition.y;
        }

        smoothPosition = Vector3.SmoothDamp(smoothPosition, targetPos, ref velocity, smoothDampTime);
        transform.position = Snap(smoothPosition);
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