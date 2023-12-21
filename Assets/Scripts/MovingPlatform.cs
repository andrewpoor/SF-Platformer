using UnityEngine;

//Basic moving platform that moves back and forth in a straight line.
public class MovingPlatform : MonoBehaviour
{
    [SerializeField] private MovingSolid solidPhysics;

    [SerializeField] private float speed = 1.0f;
    [SerializeField] private Vector2 endOffset = new(2.0f, 0.0f);
    [SerializeField] private float pauseDuration = 1.0f;

    private Vector3 startPoint;
    private Vector3 endPoint;
    private float totalDistance;

    private bool paused = true;
    private float pauseTimer = 0.0f;

    void Start()
    {
        startPoint = transform.position;
        endPoint = startPoint + new Vector3(endOffset.x, endOffset.y, 0.0f);
        totalDistance = endOffset.magnitude;
    }

    void FixedUpdate()
    {
        if(paused)
        {
            //Wait until pause timer finished.
            pauseTimer += Time.fixedDeltaTime;
            if(pauseTimer >= pauseDuration)
            {
                paused = false;
                pauseTimer = 0.0f;
            }
        }

        if(!paused)
        {
            //Move for this frame.
            float curDistance = (transform.position - startPoint).magnitude;
            float nextDistance = curDistance + speed * Time.fixedDeltaTime;
            Vector3 movement = Vector3.Lerp(startPoint, endPoint, nextDistance / totalDistance) - transform.position;
            solidPhysics.Move(movement.x, movement.y);

            //If at the end, swap end points and pause for a moment.
            if(nextDistance >= totalDistance)
            {
                (startPoint, endPoint) = (endPoint, startPoint);
                paused = true;
            }
        }
    }
}
