using UnityEngine;

public class CameraController : MonoBehaviour
{
    //References.
    [SerializeField] private Transform anchor; //Camera follows this point.

    //Parameters.
    [SerializeField] private float maxSpeed = 10.0f; //If the camera has to move a lot to catch up, limit its speed.

    void Start()
    {
        if(anchor == null)
        {
            //Follow player by default.
            GameObject playerAnchor = GameObject.FindWithTag("PlayerCamAnchor");
            if(playerAnchor != null)
            {
                anchor = playerAnchor.transform;
            }
        }
    }

    void FixedUpdate()
    {
        //Update position to follow the anchor.
        if(anchor != null)
        {
            float maxStepDistance = maxSpeed * Time.fixedDeltaTime; //Max amount camera can move in a single frame.
            float xDelta = capValue(anchor.position.x - transform.position.x, maxStepDistance);
            float yDelta = capValue(anchor.position.y - transform.position.y, maxStepDistance);

            if(xDelta != 0.0f)
            {
                transform.position += new Vector3(xDelta, 0.0f, 0.0f);
            }

            if(yDelta != 0.0f)
            {
                transform.position += new Vector3(0.0f, yDelta, 0.0f);
            }
        }
    }

    //Caps a value between +/- limit. limit should be positive.
    private static float capValue(float val, float limit)
    {
        return Mathf.Max(Mathf.Min(val, limit), -limit);
    }
}
