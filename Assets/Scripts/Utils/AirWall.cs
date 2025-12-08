using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AirWall : MonoBehaviour
{
    private const float DESIGN_WIDTH = 1284f;
    private const float DESIGN_HEIGHT = 2778f;
    private float width;
    private float height;

    private void Awake()
    {
        width = GetComponent<BoxCollider2D>().size.x;
        height = GetComponent<BoxCollider2D>().size.y;

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        float scale = screenWidth / DESIGN_WIDTH;
        float scaleY = screenHeight / DESIGN_HEIGHT;
        transform.localScale = new Vector3(scale, scaleY, 1);
    }

    
    
}
