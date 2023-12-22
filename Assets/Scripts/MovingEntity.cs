using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

/*
 * Custom physics implementation for entities in a 2D platformer.
 * Handles collision, velocity/acceleration and gravity.
 * Collisions assume everything is an Axis-Aligned Bounding Box, except for sloped ground,
 *  which is a special exception. Sloped walls and ceilings will not work correctly if present 
 *  in a level, and should be avoided.
 */
public class MovingEntity : MonoBehaviour
{
    //References.
    [SerializeField] private BoxCollider2D hitbox;
    [SerializeField] private Rigidbody2D rBody;

    //Parameters.
    public float verticalDrag = 0.1f;
    public float gravityScale = 1.0f;

    //Surface contacts.
    private bool groundContact = false; //True if the entity is on the ground.
    private bool ceilingContact = false;
    private bool rightWallContact = false;
    private bool leftWallContact = false;
    private bool slopedGround = false; //True if sticking to sloped ground.

    //References to any moving solids at the contact points.
    private MovingSolid groundMovingSolid = null;
    private MovingSolid ceilingMovingSolid = null;
    private MovingSolid rightWallMovingSolid = null;
    private MovingSolid leftWallMovingSolid = null;

    private bool isRiding = false;
    private bool wasRiding = false;
    private Vector3 ridingVelocity = Vector3.zero;
    private float RIDING_VELOCITY_BUFFER_TIME = 0.1f;

    //Collisions.
    private const float SURFACE_CHECK_INSET = 0.1f; //Surface check raycasts should start inset from the bounds of the collider.
    private const float SURFACE_CHECK_DISTANCE = 0.001f; //How close a surface must be to be considered in contact with the player.
    private const float SLOPE_CHECK_DISTANCE = 0.1f; //How close a slope can be below to still stick to it.
    private const int SURFACE_LAYER_MASK = 1 << 3;
    private const float FLOOR_ANGLE = 60.0f; //How steep a surface can be to be considered ground.
    private const float WALL_ANGLE = 25.0f; //How slanted a surface can be to be considered a wall.
    private const float MOVE_UNIT = 0.01f; //Movement is split into units for better collision detection. Smaller units are more precise but expensive.
    private const float FLOOR_STICK_VELOCITY_THRESHOLD = 3.5f; //If the floor moves any slower than this, the entity should stay attached to it.

    //Control whether messages are sent on certain events.
    private bool floorMessages = false;
    private bool wallMessages = false;
    private bool launchMessages = false;

    //Movement.
    private const float GRAVITY_CONST = -9.81f;
    private Vector3 acceleration = Vector3.zero;
    private Vector3 velocity = Vector3.zero;

    private class CollisionInfo
    {
        public float distance; //Distance to collision.
        public float angle; //Angle of surface colliding with.
        public bool isMovingSolid; //True if the surface is that of a moving solid.
        public GameObject surfaceObject; //Game object that's being collided with.
    }

    void OnEnable()
    {
        LevelManager.Instance.RegisterEntity(this);
    }

    void OnDisable()
    {
        LevelManager.Instance.UnregisterEntity(this);
    }

    void FixedUpdate()
    {
        UpdateRiding();
        UpdatePhysics();
    }

    public Vector2 GetVelocity()
    {
        return velocity;
    }

    public void SetVelocity(Vector2 newVelocity)
    {
        velocity = newVelocity;
    }
    

    public void SetVelocity(float xNewVelocity, float yNewVelocity)
    {
        velocity.x = xNewVelocity;
        velocity.y = yNewVelocity;
    }

    public void SetVelocityX(float xNewVelocity)
    {
        velocity.x = xNewVelocity;
    }

    public void SetVelocityY(float yNewVelocity)
    {
        velocity.y = yNewVelocity;
    }

    //Set strength of gravity. 1.0 is the default.
    public void SetGravityScale(float scale)
    {
        gravityScale = scale;
    }

    //Tell this behaviour to send messages for ground and ceiling collisions.
    //The Game Object must implement the appropriate message receiving functions.
    //(These are OnTouchFloorand OnLeaveFloor.)
    public void EnableFloorMessages(bool enabled = true)
    {
        floorMessages = enabled;
    }

    //Tell this behaviour to send messages for wall collisions.
    //The Game Object must implement the appropriate message receiving functions.
    //(These are OnTouchWall and OnLeaveWall.)
    public void EnableWallMessages(bool enabled = true)
    {
        wallMessages = enabled;
    }

    //Tell this behaviour to send messages whenever this entity is launched by the environment.
    //The Game Object must implement the appropriate message receiving function.
    //(This is OnLaunch.)
    public void EnableLaunchMessages(bool enabled = true)
    {
        launchMessages = enabled;
    }

    //Check if there's a ceiling above the entity, within the given distance.
    public bool CheckCeilingCollision(float distance)
    {
        return TestFloorCollision(distance, true) != null;
    }

    //Check if this entity is riding the given object. The object's velocity is used as part of this check.
    public bool IsRidingObject(MovingSolid ridableObject, Vector2 objVelocity)
    {
        //An entity is considered riding an object if it's pressing against it with a velocity
        // greater than the velocity of the object.
        //Ground collision is a special case, as Y velocity is zeroed on the ground, but the entity
        // should still 'stick' to an object below if it's descending slow enough.
        bool ridingObject = 
            (ceilingMovingSolid == ridableObject && velocity.y > objVelocity.y) ||
            (groundMovingSolid == ridableObject && -FLOOR_STICK_VELOCITY_THRESHOLD < objVelocity.y) ||
            (rightWallMovingSolid == ridableObject && velocity.x > objVelocity.x) ||
            (leftWallMovingSolid == ridableObject && velocity.x < objVelocity.x);

        if(ridingObject)
        {
            isRiding = true;
            ridingVelocity = objVelocity;
        }

        return ridingObject;
    }

    //Update state of riding an object.
    //Note that the physics assumes the entity can only ever ride one object at a time.
    private void UpdateRiding()
    {
        //Check if currently riding. If so, set the flag to false. The next time this function is called,
        // if the entity is still riding an object, the flag will have been set back to true. If it's still false,
        // this indicates the entity is no longer riding an object.
        if(isRiding)
        {
            isRiding = false;
            wasRiding = true;
        }
        else if(wasRiding)
        {
            //Was riding a moving object, but no longer.
            wasRiding = false;
            StartCoroutine(DelayLoseRidingVelocity());
        }
    }

    //Called when the entity has stopped riding a moving object.
    //The velocity imparted by the moving object will remain for a brief period,
    // before being zeroed. This acts as a buffer so the player doesn't have to be
    // too precise with their inputs when making use of this velocity.
    private IEnumerator DelayLoseRidingVelocity()
    {
        yield return new WaitForSeconds(RIDING_VELOCITY_BUFFER_TIME);

        ridingVelocity = Vector3.zero;
    }

    //Resolve velocity, acceleration and collisions.
    private void UpdatePhysics()
    {
        //Calculate movement based on velocity. Uses Verlet method.
        Vector3 moveDelta = (velocity + (acceleration / 2.0f) * Time.fixedDeltaTime) * Time.fixedDeltaTime;

        //Apply movement, accounting for collisions.
        Move(moveDelta.x, moveDelta.y);

        if(moveDelta.y == 0)
        {
            //Special check for sloped ground. If there's no vertical movement, which indicates the
            // entity was grounded last frame and isn't jumping or similar, check for sloped ground
            // below and move to stay attached to it if needed.
            //(This doesn't account for sloped ceilings, only floors.)
            StickToSlope();
        }

        bool prevCeiling = ceilingContact;
        bool prevGrounded = groundContact;
        bool prevRightWall = rightWallContact;
        bool prevLeftWall = leftWallContact;
        CheckTouchingSurfaces();

        //If the entity just left the ground, impart any velocity from an object it may have been riding.
        if(prevGrounded && !groundContact && ridingVelocity.magnitude > 0.01f)
        {
            velocity += ridingVelocity;

            if(launchMessages)
            {
                gameObject.SendMessage("OnLaunch", ridingVelocity);
            }

            ridingVelocity = Vector3.zero;
        }

        //Send messages for starting or ending surface contacts.
        if(floorMessages)
        {
            if(prevCeiling && !ceilingContact)
            {
                gameObject.SendMessage("OnLeaveFloor", true);
            }
            else if(!prevCeiling && ceilingContact)
            {
                gameObject.SendMessage("OnTouchFloor", true);
            }

            if(prevGrounded && !groundContact)
            {
                gameObject.SendMessage("OnLeaveFloor", false);
            }
            else if(!prevGrounded && groundContact)
            {
                gameObject.SendMessage("OnTouchFloor", false);
            }
        }
        
        if(wallMessages)
        {
            if(prevRightWall && !rightWallContact)
            {
                gameObject.SendMessage("OnLeaveWall", true);
            }
            else if(!prevRightWall && rightWallContact)
            {
                gameObject.SendMessage("OnTouchWall", true);
            }

            if(prevLeftWall && !leftWallContact)
            {
                gameObject.SendMessage("OnLeaveWall", false);
            }
            else if(!prevLeftWall && leftWallContact)
            {
                gameObject.SendMessage("OnTouchWall", false);
            }
        }

        //Recalculate forces (gravity, drag).
        float yNewVelocity = 0.0f;
        float yNewAcceleration = 0.0f;
        bool bonk = ceilingContact && velocity.y > 0.0f; //Hitting the ceiling means upwards velocity is stopped.
        if(!groundContact && !bonk)
        {
            //Only care about gravity when off the ground.
            float drag = velocity.y * velocity.y * verticalDrag * ((velocity.y > 0.0f) ? -1 : 1);
            yNewAcceleration = GRAVITY_CONST * gravityScale + drag;

            //Verlet method.
            yNewVelocity = velocity.y + ((acceleration.y + yNewAcceleration) / 2.0f) * Time.fixedDeltaTime;
        }

        //New velocity for next frame. Can be modified by other functions in between calls to UpdatePhysics.
        //(There are no horizontal forces, so x velocity is unchanged.)
        velocity.y = yNewVelocity;
        acceleration.y = yNewAcceleration;
    }

    //Move by the given amount, accounting for collisions.
    private void Move(float xMove, float yMove)
    {
        /*
         * The movement system assumes everything is an Axis-Aligned-Bounding-Box.
         * The one exception is sloped ground, which is also accounted for. Sloped walls and ceilings are
         *  not, and will result in undesired behaviour if used in a level.
         * Movement is done in small steps, checking collisions each increment.
         * This function is moderately expensive and should only be called from FixedUpdate.
         */

        bool rightward = xMove > 0.0f;
        bool upward = yMove > 0.0f;
        float xAmount = Mathf.Abs(xMove);
        float yAmount = Mathf.Abs(yMove);
        
        //Move one unit at a time, checking collisions along the way.
        while(xAmount > 0.0f || yAmount > 0.0f)
        {
            //X first. During a corner collision, this will result in landing on the ground rather than
            // sliding down the wall.
            if(xAmount > 0.0f)
            {
                float xSmallMove = xAmount > MOVE_UNIT ? MOVE_UNIT : xAmount;
                MoveCollideX(xSmallMove, rightward);
                xAmount -= xSmallMove;
            }

            if(yAmount > 0.0f)
            {
                float ySmallMove = yAmount > MOVE_UNIT ? MOVE_UNIT : yAmount;
                MoveCollideY(ySmallMove, upward);
                yAmount -= ySmallMove;
            }
        }
    }

    //If there is a slope below the entity (within tolerance), move vertically to it.
    private void StickToSlope()
    {
        CollisionInfo groundCheck = TestFloorCollision(SLOPE_CHECK_DISTANCE, false);
        bool prevSloped = slopedGround;
        slopedGround = groundCheck != null && groundCheck.angle != 0.0f;

        //Check if over sloped ground, or if the entity just stepped off of a slope.
        //(In the latter case the entity reached the foot of a ramp and still needs to move down
        // to meet the flat ground at the bottom.)
        if(slopedGround || (groundCheck != null && prevSloped))
        {
            rBody.position -= new Vector2(0.0f, groundCheck.distance);
        }
    }

    //Check if any surfaces in the cardinal directions are in contact.
    private void CheckTouchingSurfaces()
    {
        //Collision checks in the cardinal directions.
        CollisionInfo ceilingCol = TestFloorCollision(SURFACE_CHECK_DISTANCE, true);
        CollisionInfo groundCol = TestFloorCollision(SURFACE_CHECK_DISTANCE, false);
        CollisionInfo rightWallCol = TestWallCollision(SURFACE_CHECK_DISTANCE, true);
        CollisionInfo leftWallCol = TestWallCollision(SURFACE_CHECK_DISTANCE, false);

        //First check if being squashed between two surfaces.
        //Entities should typically respond by being destroyed.
        if(groundCol != null && ceilingCol != null)
        {
            gameObject.SendMessage("OnVerticalSquash");
        }
        if(rightWallCol != null && leftWallCol != null)
        {
            gameObject.SendMessage("OnHorizontalSquash");
        }

        //Update contacts. A contact isn't valid if the entity is moving away from it.
        ceilingContact = ceilingCol != null && velocity.y > -0.01f;
        groundContact = groundCol != null && velocity.y < 0.01f;
        rightWallContact = rightWallCol != null && velocity.x > -0.01f;
        leftWallContact = leftWallCol != null && velocity.x < 0.01f;

        //Update riding objects. An object is being ridden if the entity is touching it and attached to it.
        ceilingMovingSolid = (ceilingContact && ceilingCol.isMovingSolid) ? ceilingCol.surfaceObject.GetComponent<MovingSolid>() : null;
        groundMovingSolid = (groundContact && groundCol.isMovingSolid) ? groundCol.surfaceObject.GetComponent<MovingSolid>() : null;
        rightWallMovingSolid = (rightWallContact && rightWallCol.isMovingSolid) ? rightWallCol.surfaceObject.GetComponent<MovingSolid>() : null;
        leftWallMovingSolid = (leftWallContact && leftWallCol.isMovingSolid) ? leftWallCol.surfaceObject.GetComponent<MovingSolid>() : null;
    }

    //Move horizontally by the given amount. If this would collide, stop short.
    //moveDistance should be positive. rightward indicates direction.
    //Return value indicates if there was a collision in the movement direction.
    public bool MoveCollideX(float moveDistance, bool rightward)
    {
        //Check for collision, and cap movement if it's collided.
        CollisionInfo col = TestWallCollision(moveDistance, rightward);
        bool collided = col != null;
        moveDistance = collided ? col.distance : moveDistance;

        //Move the appropriate amount.
        rBody.position += new Vector2(rightward ? moveDistance : -moveDistance, 0.0f);

        return collided;
    }

    //Move vertically by the given amount. If this would collide, stop short.
    //moveDistance should be positive. upward indicates direction.
    //Return value indicates if there was a collision in the movement direction.
    public bool MoveCollideY(float moveDistance, bool upward)
    {
        //Check for collision, and cap movement if it's collided.
        CollisionInfo col = TestFloorCollision(moveDistance, upward);
        bool collided = col != null;
        moveDistance = collided ? col.distance : moveDistance;

        //Move the appropriate amount.
        rBody.position += new Vector2(0.0f, upward ? moveDistance : -moveDistance);

        return collided;
    }

    //See if the player collides with a floor/ceiling surface some distance away.
    //Either check the ceiling above or the ground below.
    //Return value is an object containing information on the collision. It's null if there was no collision.
    private CollisionInfo TestFloorCollision(float distance, bool lookUp)
    {
        float yOrigin = lookUp ? hitbox.bounds.max.y : hitbox.bounds.min.y;
        Vector2 leftOrigin = new Vector2(hitbox.bounds.min.x + 0.005f, yOrigin);
        Vector2 rightOrigin = new Vector2(hitbox.bounds.max.x - 0.005f, yOrigin);
        Vector2 dir = lookUp ? Vector2.up : Vector2.down;

        CollisionInfo leftCol = SurfaceRayTest(leftOrigin, dir, distance, FLOOR_ANGLE);
        CollisionInfo rightCol = SurfaceRayTest(rightOrigin, dir, distance, FLOOR_ANGLE);

        if(rightCol == null)
        {
            return leftCol; //Returns null if both are null.
        }
        else if(leftCol == null)
        {
            return rightCol;
        }
        else
        {
            //Consolidate info from all collisions.
            CollisionInfo nearestCol = leftCol.distance < rightCol.distance ? leftCol : rightCol;
            nearestCol.isMovingSolid = leftCol.isMovingSolid && rightCol.isMovingSolid;
            return nearestCol;
        }
    }

    //See if the player collides with a wall some distance away.
    //Either check for a wall to the right or the left.
    //Return value is an object containing information on the collision. It's null if there was no collision.
    private CollisionInfo TestWallCollision(float distance, bool lookRight)
    {
        float xOrigin = lookRight ? hitbox.bounds.max.x : hitbox.bounds.min.x;
        Vector2 topOrigin = new Vector2(xOrigin, hitbox.bounds.max.y - 0.005f);
        Vector2 middleOrigin = new Vector2(xOrigin, hitbox.bounds.center.y);
        Vector2 bottomOrigin = new Vector2(xOrigin, hitbox.bounds.min.y + 0.005f);
        Vector2 dir = lookRight ? Vector2.right : Vector2.left;

        CollisionInfo topCol = SurfaceRayTest(topOrigin, dir, distance, WALL_ANGLE);
        CollisionInfo middleCol = SurfaceRayTest(middleOrigin, dir, distance, WALL_ANGLE);
        CollisionInfo bottomCol = SurfaceRayTest(bottomOrigin, dir, distance, WALL_ANGLE);

        if(topCol == null && middleCol == null && bottomCol == null)
        {
            return null;
        }
        else
        {
            //Consolidate info from all collisions.
            CollisionInfo nearestCol = topCol;
            nearestCol = (nearestCol == null || (middleCol != null && middleCol.distance < nearestCol.distance)) ? middleCol : nearestCol;
            nearestCol = (nearestCol == null || (bottomCol != null && bottomCol.distance < nearestCol.distance)) ? bottomCol : nearestCol;

            //Only register moving solid contact if every valid contact point is touching it.
            nearestCol.isMovingSolid = 
                    (topCol == null || topCol.isMovingSolid) && 
                    (middleCol == null || middleCol.isMovingSolid) && 
                    (bottomCol == null || bottomCol.isMovingSolid);

            return nearestCol;
        }
    }

    //Helper function that casts a ray in a given direction, and determines if a surface of the correct
    // orientation is hit.
    //A hit surface must be angled within the given tolerance to be valid.
    // E.g. if the ray is cast downwards to check for the ground, it can't be too steep, otherwise it 
    // wouldn't count as 'ground'.
    //The ray starts from an inset position, to detect collisions inside, or at the bounds of, this hitbox.
    //Return value is an object containing information on the collision. It's null if there was no collision.
    private CollisionInfo SurfaceRayTest(Vector2 origin, Vector2 direction, float distance, float angleTolerance)
    {
        RaycastHit2D hit = Physics2D.Raycast(
            origin - direction * SURFACE_CHECK_INSET,
            direction,
            SURFACE_CHECK_INSET + distance,
            SURFACE_LAYER_MASK);
        
        //Check if it hit a surface, and if so, that the surface angle is within tolerance.
        //(Ignore hits from colliders inside the ray's origin, as no normal is computed in that instance.)
        if((hit.collider != null) && (hit.fraction != 0.0f))
        {
            float colAngle = Mathf.Abs(Vector2.Angle(-direction, hit.normal));
            if(colAngle <= angleTolerance)
            {
                return new CollisionInfo()
                {
                    angle = colAngle,
                    distance = hit.distance - SURFACE_CHECK_INSET,
                    isMovingSolid = hit.collider.CompareTag("MovingSolid"),
                    surfaceObject = hit.collider.gameObject
                };
            }
        }
    
        return null;
    }
}
