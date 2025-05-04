using System.Collections;
using UnityEngine;

public class ArrowSelector : MonoBehaviour
{
    [System.Serializable]
    public struct ButtonData
    {
        public RectTransform button;
        public Vector2 arrowOffset;
    }

    public bool showDebugLinesOnlyOnActiveObjects = true;
    [SerializeField] ButtonData[] buttons;
    [SerializeField] RectTransform arrowIndicator;
    [HideInInspector] public bool isSelectingOption = false;

    [HideInInspector] public int lastSelected = -1;
    bool firstFrame = true;

    void LateUpdate()
    {
        if (firstFrame)
        {
            firstFrame = false;
        }

        if (UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == null || isSelectingOption)
    {
        arrowIndicator.gameObject.SetActive(false);
    }
    }

    public void PointerEnter(int b)
    {
        // MoveIndicator(b);
    }

    public void PointerExit(int b)
    {
        // MoveIndicator(lastSelected);
    }

    public void ButtonSelected(int b)
    {
        lastSelected = b;
        MoveIndicator(b);
    }

    public void MoveIndicator(int b)
    {
        if (isSelectingOption || firstFrame)
        {
            StartCoroutine(MoveIndicatorLaterCoroutine(b));
            return;
        }

        if (b < 0 || b >= buttons.Length || buttons[b].button == null)
        {
            arrowIndicator.gameObject.SetActive(false);
            return;
        }

        arrowIndicator.gameObject.SetActive(true);
        Vector3 worldPos = buttons[b].button.TransformPoint((Vector3)buttons[b].arrowOffset); // keep this in mind for other projects
        arrowIndicator.position = worldPos;
    }

    IEnumerator MoveIndicatorLaterCoroutine(int b)
    {
        yield return null;
        MoveIndicator(b);
    }

    public void SetSelecting(bool value)
    {
        isSelectingOption = value;
    }

    void OnDrawGizmos()
    {
        if (buttons == null || buttons.Length == 0) return;

        for (int i = 0; i < buttons.Length; i++)
        {
            RectTransform rect = buttons[i].button;
            if (rect == null) continue;
            if (showDebugLinesOnlyOnActiveObjects && !rect.gameObject.activeInHierarchy) continue;

            // button base position (local space converted to world)
            Vector3 buttonWorldPos = rect.TransformPoint(Vector3.zero);

            // local space offset position converted to world
            Vector3 offsetWorldPos = rect.TransformPoint(buttons[i].arrowOffset);

            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(buttonWorldPos, 5f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(offsetWorldPos, 7f);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(buttonWorldPos, offsetWorldPos);
        }

        if (lastSelected >= 0 && lastSelected < buttons.Length && buttons[lastSelected].button != null)
        {
            RectTransform selectedRect = buttons[lastSelected].button;
            Vector3 selectedWorldPos = selectedRect.TransformPoint(buttons[lastSelected].arrowOffset);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(selectedWorldPos, 10f);
        }
    }
}