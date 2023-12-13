using UnityEngine;

/*
 * Custom physics implementation for entities in a 2D platformer.
 * Handles collision, velocity/acceleration and gravity.
 * Collisions assume everything is an Axis-Aligned Bounding Box, except for sloped ground,
 *  which is a special exception. Sloped walls and ceilings will not work correctly if present 
 *  in a level, and should be avoided.
 */
public class PlatformerPhysics : MonoBehaviour
{
    //References.
    public BoxCollider2D hitbox;

    //Parameters.
    public float verticalDrag = 0.1f;
    public float gravityScale = 1.0f;

    //Surface contacts.
    private bool groundContact = false; //True if the entity is on the ground.
    private bool ceilingContact = false;
    private bool rightWallContact = false;
    private bool leftWallContact = false;
    private bool slopedGround = false; //True if sticking to sloped ground.

    //Collisions.
    private const float SURFACE_CHECK_INSET = 0.1f; //Surface check raycasts should start inset from the bounds of the collider.
    private const float SURFACE_CHECK_DISTANCE = 0.001f; //How close a surface must be to be considered in contact with the player.
    private const float SLOPE_CHECK_DISTANCE = 0.1f; //How close a slope can be below to still stick to it.
    private const int SURFACE_LAYER_MASK = 1 << 3;
    private const float FLOOR_ANGLE = 60.0f; //How steep a surface can be to be considered ground.
    private const float WALL_ANGLE = 25.0f; //How slanted a surface can be to be considered a wall.
    private const float MOVE_UNIT = 0.01f; //Movement is split into units for better collision detection. Smaller units are more precise but expensive.

    //Hitbox cached dimensions. The actual hitbox doesn't sync with the actor's location
    // until a later frame, so values should be saved at the start of every fixed update
    // in order to provide accurate results when used mid-frame.
    private float hitboxTopOffset;
    private float hitboxBottomOffset;
    private float hitboxLeftOffset;
    private float hitboxRightOffset;

    //Control whether messages are sent on collision events.
    private bool floorMessages = false;
    private bool wallMessages = false;

    //Movement.
    private const float GRAVITY_CONST = -9.81f;
    public Vector3 acceleration = new Vector3(0.0f, 0.0f, 0.0f);
    public Vector3 velocity = new Vector3(0.0f, 0.0f, 0.0f);

    private class CollisionInfo
    {
        public float distance; //Distance to collision.
        public float angle; //Angle of surface colliding with.
    }

    void FixedUpdate()
    {
        CacheHitboxData();

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

    //Check if there's a ceiling above the entity, within the given distance.
    public bool CheckCeilingCollision(float distance)
    {
        return TestFloorCollision(distance, true) < Mathf.Infinity;
    }

    //Store current values for hitbox information.
    private void CacheHitboxData()
    {
        hitboxTopOffset = hitbox.bounds.max.y - transform.position.y;
        hitboxBottomOffset = hitbox.bounds.min.y - transform.position.y;
        hitboxLeftOffset = hitbox.bounds.min.x - transform.position.x;
        hitboxRightOffset = hitbox.bounds.max.x - transform.position.x;
    }

    //Resolve velocity, acceleration and collisions.
    private void UpdatePhysics()
    {
        //Calculate movement based on velocity. Uses Verlet method.
        Vector3 moveDelta = (velocity + (acceleration / 2.0f) * Time.fixedDeltaTime) * Time.fixedDeltaTime;

        //Apply movement, accounting for collisions.
        MoveWithCollisions(moveDelta.x, moveDelta.y);

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
    private void MoveWithCollisions(float xMove, float yMove)
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
        CollisionInfo groundCheck = TestGroundCollisionEx(SLOPE_CHECK_DISTANCE);
        bool prevSloped = slopedGround;
        slopedGround = groundCheck != null && groundCheck.angle != 0.0f;

        //Check if over sloped ground, or if the entity just stepped off of a slope.
        //(In the latter case the entity reached the foot of a ramp and still needs to move down
        // to meet the flat ground at the bottom.)
        if(slopedGround || (groundCheck != null && prevSloped))
        {
            transform.Translate(0.0f, -groundCheck.distance, 0.0f);
        }
    }

    //Check if any surfaces in the cardinal directions are in contact.
    private void CheckTouchingSurfaces()
    {
        //Collision checks in the cardinal directions.
        bool ceilingHit = TestFloorCollision(SURFACE_CHECK_DISTANCE, true) < Mathf.Infinity;
        bool groundHit = TestFloorCollision(SURFACE_CHECK_DISTANCE, false) < Mathf.Infinity;
        bool rightWallHit = TestWallCollision(SURFACE_CHECK_DISTANCE, true) < Mathf.Infinity;
        bool leftWallHit = TestWallCollision(SURFACE_CHECK_DISTANCE, false) < Mathf.Infinity;

        //First check if being squashed between two surfaces.
        //Entities should typically respond by being destroyed.
        if(groundHit && ceilingHit)
        {
            gameObject.SendMessage("OnVerticalSquash");
        }
        if(rightWallHit && leftWallHit)
        {
            gameObject.SendMessage("OnHorizontalSquash");
        }

        //Update contacts. A contact isn't valid if the entity is moving away from it.
        ceilingContact = ceilingHit && velocity.y > -0.01f;
        groundContact = groundHit && velocity.y < 0.01f;
        rightWallContact = rightWallHit && velocity.x > -0.01f;
        leftWallContact = leftWallHit && velocity.x < 0.01f;
    }

    //Move horizontally by the given amount. If this would collide, stop short.
    //amount should be positive. rightward indicates direction.
    //Return value indicates if there was a collision in the movement direction.
    private bool MoveCollideX(float moveDistance, bool rightward)
    {
        //Check for collision, and cap movement if it's collided.
        float colDistance = TestWallCollision(moveDistance, rightward);
        bool collided = colDistance < moveDistance;
        moveDistance = collided ? colDistance : moveDistance;

        //Move the appropriate amount.
        transform.Translate(rightward ? moveDistance : -moveDistance, 0.0f, 0.0f);

        return collided;
    }

    //Move vertically by the given amount. If this would collide, stop short.
    //amount should be positive. upward indicates direction.
    //Return value indicates if there was a collision in the movement direction.
    private bool MoveCollideY(float moveDistance, bool upward)
    {
        //Check for collision, and cap movement if it's collided.
        float colDistance = TestFloorCollision(moveDistance, upward);
        bool collided = colDistance < moveDistance;
        moveDistance = collided ? colDistance : moveDistance;

        //Move the appropriate amount.
        transform.Translate(0.0f, upward ? moveDistance : -moveDistance, 0.0f);

        return collided;
    }

    //See if the player collides with a floor/ceiling surface some distance away.
    //Either check the ceiling above or the ground below.
    private float TestFloorCollision(float distance, bool lookUp)
    {
        float yOrigin = transform.position.y + (lookUp ? hitboxTopOffset : hitboxBottomOffset);
        Vector2 leftOrigin = new Vector2(transform.position.x + hitboxLeftOffset + 0.005f, yOrigin);
        Vector2 rightOrigin = new Vector2(transform.position.x + hitboxRightOffset - 0.005f, yOrigin);
        Vector2 dir = lookUp ? Vector2.up : Vector2.down;

        float colDistance = Mathf.Infinity;
        colDistance = Mathf.Min(colDistance, SurfaceRayTest(leftOrigin, dir, distance, FLOOR_ANGLE));
        colDistance = Mathf.Min(colDistance, SurfaceRayTest(rightOrigin, dir, distance, FLOOR_ANGLE));

        return colDistance;
    }

    //Special ground collision check that returns more information on the collision.
    private CollisionInfo TestGroundCollisionEx(float distance)
    {
        float yOrigin = transform.position.y + hitboxBottomOffset;
        Vector2 leftOrigin = new Vector2(transform.position.x + hitboxLeftOffset + 0.005f, yOrigin);
        Vector2 rightOrigin = new Vector2(transform.position.x + hitboxRightOffset - 0.005f, yOrigin);

        CollisionInfo leftCol = SurfaceRayTestEx(leftOrigin, Vector2.down, distance, FLOOR_ANGLE);
        CollisionInfo rightCol = SurfaceRayTestEx(rightOrigin, Vector2.down, distance, FLOOR_ANGLE);

        if(leftCol == null && rightCol == null)
        {
            return null;
        }
        else if(rightCol == null || (leftCol != null && leftCol.distance < rightCol.distance))
        {
            return leftCol;
        }
        else
        {
            return rightCol;
        }
    }

    //See if the player collides with a wall some distance away.
    //Either check for a wall to the right or the left.
    private float TestWallCollision(float distance, bool lookRight)
    {
        float xOrigin = transform.position.x + (lookRight ? hitboxRightOffset : hitboxLeftOffset);
        Vector2 topOrigin = new Vector2(xOrigin, transform.position.y + hitboxTopOffset - 0.005f);
        Vector2 middleOrigin = new Vector2(xOrigin, transform.position.y);
        Vector2 bottomOrigin = new Vector2(xOrigin, transform.position.y + hitboxBottomOffset + 0.005f);
        Vector2 dir = lookRight ? Vector2.right : Vector2.left;

        float colDistance = Mathf.Infinity;
        colDistance = Mathf.Min(colDistance, SurfaceRayTest(topOrigin, dir, distance, WALL_ANGLE));
        colDistance = Mathf.Min(colDistance, SurfaceRayTest(middleOrigin, dir, distance, WALL_ANGLE));
        colDistance = Mathf.Min(colDistance, SurfaceRayTest(bottomOrigin, dir, distance, WALL_ANGLE));

        return colDistance;
    }

    //Helper function that casts a ray in a given direction, and determines if a surface of the correct
    // orientation is hit.
    //A hit surface must be angled within the given tolerance to be valid.
    // E.g. if the ray is cast downwards to check for the ground, it can't be too steep, otherwise it 
    // wouldn't count as 'ground'.
    //The ray starts from an inset position, to detect collisions inside, or at the bounds of, this hitbox.
    //Return value is the distance to the collision; Infinity otherwise.
    private float SurfaceRayTest(Vector2 origin, Vector2 direction, float distance, float angleTolerance)
    {
        RaycastHit2D hit = Physics2D.Raycast(
            origin - direction * SURFACE_CHECK_INSET,
            direction,
            SURFACE_CHECK_INSET + distance,
            SURFACE_LAYER_MASK);
        
        //Check if it hit a surface, and if so, that the surface angle is within tolerance.
        //(Ignore hits from colliders inside the ray's origin, as no normal is computed in that instance.)
        if( (hit.collider != null) && 
            (hit.fraction != 0.0f) && 
            (Mathf.Abs(Vector2.Angle(-direction, hit.normal)) <= angleTolerance))
        {
            return hit.distance - SURFACE_CHECK_INSET;
        }
        else
        {
            return Mathf.Infinity;
        }
    }

    //Surface ray test that returns more information about the collision.
    private CollisionInfo SurfaceRayTestEx(Vector2 origin, Vector2 direction, float distance, float angleTolerance)
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
                    distance = hit.distance - SURFACE_CHECK_INSET
                };
            }
        }
    
        return null;
    }
}
