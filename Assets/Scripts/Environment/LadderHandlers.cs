using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A ladder: works out its top, bottom and platform heights from its trigger. Players find it themselves
/// (MegamanLadder looks for the ladder overlapping them), so several can use it at once.
/// </summary>
public class LadderHandlers : MonoBehaviour
{
    // these offsets are for reaching the top (climb up)
    // and bottom (to let go and fall off) of the ladder
    public float handlerTopOffset = 0.04f;
    public float handlerBottomOffset = 0.04f;

    // these positions are for the player's ladder climbing logic
    [HideInInspector] public float posX;
    [HideInInspector] public float posTopY;
    [HideInInspector] public float posBottomY;
    [HideInInspector] public float posTopHandlerY;
    [HideInInspector] public float posBottomHandlerY;
    [HideInInspector] public float posPlatformY;

    void Awake()
    {
        // set up the positioning
        GetComponent<BoxCollider2D>().offset = Vector2.zero;
        Vector2 size = GetComponent<BoxCollider2D>().size;
        Transform ladderTop = transform.GetChild(0).transform;
        Transform ladderBottom = transform.GetChild(1).transform;
        Transform ladderPlatform = transform.GetChild(2).transform;
        ladderTop.position = new Vector3(transform.position.x, transform.position.y + (size.y / 2), 0);
        ladderBottom.position = new Vector3(transform.position.x, transform.position.y - (size.y / 2), 0);
        ladderPlatform.position = new Vector3(transform.position.x, transform.position.y + (size.y / 2), 0);

        // set up the variables for the climber to use
        posX = transform.position.x;
        posTopY = ladderTop.transform.position.y;
        posBottomY = ladderBottom.transform.position.y;
        posPlatformY = ladderPlatform.transform.position.y;
        posTopHandlerY = posTopY + handlerTopOffset;
        posBottomHandlerY = posBottomY + handlerBottomOffset;
    }
}
