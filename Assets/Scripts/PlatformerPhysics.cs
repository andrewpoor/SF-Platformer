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
    public float verticalDrag = 0.1f;
    public float gravityScale = 1.0f;

    public BoxCollider2D hitbox;

    //Surface contacts.
    private bool grounded = false; //True if the player is on the ground.

    //Collisions.
    private const float SURFACE_CHECK_INSET = 0.1f; //Surface check raycasts should start inset from the bounds of the collider.
    private const float SURFACE_CHECK_DISTANCE = 0.001f; //How close a surface must be to be considered in contact with the player.
    private const int SURFACE_LAYER_MASK = 1 << 3;
    private const float FLOOR_ANGLE = 64.0f; //How steep a surface can be to be considered ground.
    private const float WALL_ANGLE = 25.0f; //How slanted a surface can be to be considered a wall.
    private const float MOVE_UNIT = 0.01f; //Movement is split into units for better collision detection. Smaller units are more precise but expensive.

    //Movement.
    private const float GRAVITY_CONST = -9.81f;
    private Vector3 acceleration = new Vector3(0.0f, 0.0f, 0.0f);
    private Vector3 velocity = new Vector3(0.0f, 0.0f, 0.0f);

    void FixedUpdate()
    {
        UpdatePhysics();
    }

    public bool IsGrounded()
    {
        return grounded;
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

    //Resolve velocity, acceleration and collisions.
    private void UpdatePhysics()
    {
        //Calculate movement based on velocity. Uses Verlet method.
        Vector3 moveDelta = (velocity + (acceleration / 2.0f) * Time.fixedDeltaTime) * Time.fixedDeltaTime;

        //Apply movement, accounting for collisions.
        MoveWithCollisions(moveDelta.x, moveDelta.y);

        bool prevGrounded = grounded;
        CheckTouchingSurfaces();

        //Send messages if just landed or left the ground.
        if(prevGrounded && !grounded)
        {
            gameObject.SendMessage("OnLeaveGround");
        }
        else if(!prevGrounded && grounded)
        {
            gameObject.SendMessage("OnLanded");
        }

        //Recalculate forces (gravity, drag).
        float yNewVelocity = 0.0f;
        float yNewAcceleration = 0.0f;
        if(!grounded)
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
        while(xAmount > MOVE_UNIT || yAmount > MOVE_UNIT)
        {
            //X first. During a corner collision, this will result in landing on the ground rather than
            // sliding down the wall.
            if(xAmount > MOVE_UNIT)
            {
                bool collided = MoveCollideX(MOVE_UNIT, rightward);
                xAmount = collided ? 0.0f : (xAmount - MOVE_UNIT);
            }

            if(yAmount > MOVE_UNIT)
            {
                bool collided = MoveCollideY(MOVE_UNIT, upward);
                yAmount = collided ? 0.0f : (yAmount - MOVE_UNIT);
            }
        }

        //Move any remaining sub-unit distance.
        if(xAmount != 0.0f)
        {
            MoveCollideX(xAmount, rightward);
        }

        if(yAmount != 0.0f)
        {
            MoveCollideY(yAmount, upward);
        }
        else if(yMove == 0.0f)
        {
            //Special check for sloped ground. Always push up out of the floor
            // when there's not any other y movement. (This doesn't account for
            // sloped ceilings, only floors.)
            MoveCollideY(0.0f, false);
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
        if((groundHit && ceilingHit) || (rightWallHit && leftWallHit))
        {
            //Do something here, probably destroy entity.
            return;
        }

        //Update contacts. A contact isn't valid if the entity is moving away from it.
        grounded = groundHit && velocity.y < 0.01f;
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
        float yOffset = lookUp ? hitbox.bounds.max.y : hitbox.bounds.min.y;
        Vector2 leftOrigin = new Vector2(hitbox.bounds.min.x + 0.005f, yOffset);
        Vector2 rightOrigin = new Vector2(hitbox.bounds.max.x - 0.005f, yOffset);
        Vector2 dir = lookUp ? Vector2.up : Vector2.down;

        float colDistance = Mathf.Infinity;
        colDistance = Mathf.Min(colDistance, SurfaceRayTest(leftOrigin, dir, distance, FLOOR_ANGLE));
        colDistance = Mathf.Min(colDistance, SurfaceRayTest(rightOrigin, dir, distance, FLOOR_ANGLE));

        return colDistance;
    }

    //See if the player collides with a wall some distance away.
    //Either check for a wall to the right or the left.
    private float TestWallCollision(float distance, bool lookRight)
    {
        float xOffset = lookRight ? hitbox.bounds.max.x : hitbox.bounds.min.x;
        Vector2 topOrigin = new Vector2(xOffset, hitbox.bounds.max.y - 0.005f);
        Vector2 middleOrigin = new Vector2(xOffset, hitbox.bounds.center.y);
        Vector2 bottomOrigin = new Vector2(xOffset, hitbox.bounds.min.y + 0.005f);
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
}
