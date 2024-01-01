using UnityEngine;

public class CameraBlocker : MonoBehaviour
{
    void Start()
    {
        //Renderer is only for the editor, so disable it during play.
        GetComponent<SpriteRenderer>().enabled = false;
    }
}
