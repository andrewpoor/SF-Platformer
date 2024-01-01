using UnityEngine;

public class CameraController : MonoBehaviour
{
    //References.
    [SerializeField] private Transform anchor; //Camera follows this point.
    [SerializeField] private Camera cam;

    //Parameters.
    [SerializeField] private float maxSpeed = 10.0f; //If the camera has to move a lot to catch up, limit its speed.

    //Box used for collision detection. Smaller than actual view size to avoid colliding with parallel surfaces.
    private const float INSET = 0.01f; //Amount box is inset on each direction.
    private const int BLOCKER_LAYER_MASK = 1 << 7;
    private Vector2 boxCastSize;

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

        float viewHeight = cam.orthographicSize * 2.0f;
        float viewWidth = viewHeight * cam.aspect;
        boxCastSize = new Vector2(viewWidth - 2.0f * INSET, viewHeight - 2.0f * INSET);
    }

    void FixedUpdate()
    {
        //Update position to follow the anchor.
        if(anchor != null)
        {
            float maxStepDistance = maxSpeed * Time.fixedDeltaTime; //Max amount camera can move in a single frame.
            float xDelta = CapValue(anchor.position.x - transform.position.x, maxStepDistance);
            float yDelta = CapValue(anchor.position.y - transform.position.y, maxStepDistance);

            if(xDelta != 0.0f)
            {
                bool rightward = xDelta > 0.0f;
                float colDistance = CapDistanceCollisions(Mathf.Abs(xDelta), rightward ? Vector2.right : Vector2.left);
                transform.position += new Vector3(rightward ? colDistance : -colDistance, 0.0f, 0.0f);
            }

            if(yDelta != 0.0f)
            {
                bool upward = yDelta > 0.0f;
                float colDistance = CapDistanceCollisions(Mathf.Abs(yDelta), upward ? Vector2.up : Vector2.down);
                transform.position += new Vector3(0.0f, upward ? colDistance : -colDistance, 0.0f);
            }
        }
    }

    //Helper function that simulates moving the camera in the given direction by a given distance,
    // and returns the actual distance that would be moved after accounting for collisions.
    //distance should be positive. Return value is also positive.
    private float CapDistanceCollisions(float distance, Vector2 dir)
    {
        //Simulate movement with a boxcast.
        RaycastHit2D hit = Physics2D.BoxCast(transform.position, boxCastSize, 0.0f,
                dir, distance + INSET, BLOCKER_LAYER_MASK);

        //Cap movement if the camera would collide with a blocker.
        if(hit.collider != null)
        {
            distance = Mathf.Min(distance, hit.distance - INSET);
        }

        return distance;
    }

    //Caps a value between +/- limit.
    //limit should be positive.
    private float CapValue(float val, float limit)
    {
        return Mathf.Max(Mathf.Min(val, limit), -limit);
    }
}
