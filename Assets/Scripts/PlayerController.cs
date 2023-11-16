using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private class CollisionInfo
    {
        public Vector2 pos; //Position of collision.

        public CollisionInfo(Vector2 pos)
        {
            this.pos = pos;
        }
    }

    public float horizontalSpeed;
    public float jumpSpeed;
    public float verticalDrag;
    public float gravityScale;

    public BoxCollider2D boxcollider2d;
    public Animator animator;

    private bool grounded = false; //True if the player is on the ground.
    private bool prevGrounded = false; //Value of grounded variable in previous frame.
    private bool jumping = false; //Signals the animator to jump.
    private bool jumpEnabled = false;

    private bool jumpInput = false; //True indicates a jump input is waiting to be processed.
    private const float JUMP_INPUT_BUFFER_TIME = 0.15f;

    private const float SURFACE_CHECK_BUFFER = 0.1f; //Surface check raycasts should start inset from the bounds of the collider.
    private const float SURFACE_CHECK_DISTANCE = 0.001f + SURFACE_CHECK_BUFFER; //How close a surface must be to be considered in contact with the player.
    private const int SURFACE_LAYER_MASK = 1 << 3;
    private const float GROUND_ANGLE = 64.0f; //How steep a surface can be to be considered ground.
    private const float WALL_ANGLE = 25.0f; //How slanted a surface can be to be considered a wall.

    private const float GRAVITY_CONST = -9.81f;
    private Vector3 acceleration = new Vector3(0.0f, 0.0f, 0.0f);
    private Vector3 velocity = new Vector3(0.0f, 0.0f, 0.0f); 

    private enum CollisionSurface
    {
        Ground,
        RightWall,
        LeftWall,
        Ceiling
    }

    void Start()
    {
        StartCoroutine(ProcessJumpInputs());
    }

    void Update()
    {
        UpdatePhysics();
        UpdateMovement();
        UpdateAnimations();
    }

    //State machine for processing jump inputs.
    private IEnumerator ProcessJumpInputs()
    {
        while(true)
        {
            //Wait until jump is pressed.
            while(Input.GetAxisRaw("Jump") < 0.1f)
            {
                yield return null;
            }

            jumpInput = true;

            //Buffer input for a brief period. Ignore repeat presses during this time.
            yield return new WaitForSeconds(JUMP_INPUT_BUFFER_TIME);

            jumpInput = false;

            //Wait until jump is released.
            while(Input.GetAxisRaw("Jump") > 0.1f)
            {
                yield return null;
            }
        }
    }

    private void UpdateAnimations()
    {
        float rawX = Input.GetAxisRaw("Horizontal");
        animator.SetFloat("HorizontalMove", Mathf.Abs(rawX));

        //Flip the sprite if moving the other direction.
        if((rawX > 0.1f && transform.localScale.x < 0.0f) || (rawX < -0.1f && transform.localScale.x > 0.0f))
        {
            Vector3 curScale = transform.localScale;
            transform.localScale = new Vector3(-curScale.x, curScale.y, curScale.z);
        }

        animator.SetBool("Jumping", jumping);
        jumping = false;

        animator.SetBool("Falling", !grounded);
    }

    //Resolve velocity, acceleration and collisions.
    private void UpdatePhysics()
    {
        //Calculate potential movement based on velocity. Uses Verlet method.
        Vector3 moveDelta = (velocity + (acceleration / 2.0f) * Time.deltaTime) * Time.deltaTime;

        //Actual movement, accounting for collisions.
        transform.Translate(HandleCollisions(moveDelta));

        //Recalculate forces (gravity, drag).
        float yNewVelocity = 0.0f;
        float yNewAcceleration = 0.0f;
        if(!grounded)
        {
            //Only care about gravity when off the ground.
            float drag = velocity.y * velocity.y * verticalDrag * ((velocity.y > 0.0f) ? -1 : 1);
            yNewAcceleration = GRAVITY_CONST * gravityScale + drag;

            //Verlet method.
            yNewVelocity = velocity.y + ((acceleration.y + yNewAcceleration) / 2.0f) * Time.deltaTime;
        }

        //New velocity for next frame. Can be modified by other functions in between calls to UpdatePhysics.
        //(There are no horizontal forces, so x velocity is unchanged.)
        velocity.y = yNewVelocity;
        acceleration.y = yNewAcceleration;
    }

    //Handle collisions for the player moving by the given delta.
    //Returns the correct delta after accounting for collisions.
    //moveDelta must be less than SURFACE_CHECK_BUFFER to avoid passing right through a surface.
    private Vector3 HandleCollisions(Vector3 moveDelta)
    {
        //Check each surface for collision.
        CollisionInfo groundCol = TestCollidingGround(moveDelta, true);
        CollisionInfo ceilingCol = TestCollidingGround(moveDelta, false);
        CollisionInfo rightCol = TestCollidingWall(moveDelta, true);
        CollisionInfo leftCol = TestCollidingWall(moveDelta, false);

        bool groundHit = groundCol != null;
        bool ceilingHit = ceilingCol != null;
        bool rightWallHit = rightCol != null;
        bool leftWallHit = leftCol != null;

        //First check if being squashed between two surfaces.
        if((groundHit && ceilingHit) || (rightWallHit && leftWallHit))
        {
            //Do something here, probably destroy entity.
            return Vector3.zero;
        }

        //Ceiling/floor collisions. (Not a collision if moving away.)
        grounded = false;
        if(groundHit && velocity.y < 0.01f)
        {
            moveDelta.y = groundCol.pos.y - boxcollider2d.bounds.min.y;
            grounded = true;
        }
        if(ceilingHit && velocity.y > -0.01)
        {
            moveDelta.y = ceilingCol.pos.y - boxcollider2d.bounds.max.y;
        }

        //Wall collisions.
        if(rightWallHit && velocity.x > -0.01f)
        {
            moveDelta.x = rightCol.pos.x - boxcollider2d.bounds.max.x;
        }
        if(leftWallHit && velocity.x < 0.01f)
        {
            moveDelta.x = leftCol.pos.x - boxcollider2d.bounds.min.x;
        }

        return moveDelta;
    }

    //Respond to user input and react to physics calculations.
    private void UpdateMovement()
    {
        //Check if the player just landed, to re-enable jump.
        if(!prevGrounded && grounded)
        {
            //Wait before enabling the jump. This adds a brief delay, to allow
            // collision physics to act first.
            StartCoroutine(DelayJumpEnabled(true));
        }

        //Check if the player just left the ground, to disable jump.
        if(prevGrounded && !grounded)
        {
            //Wait before disabling the jump. This provides a small window
            // during which the player can still jump after falling off an edge.
            StartCoroutine(DelayJumpEnabled(false));
        }

        //Horizontal movement. Only move when input is pressed.
        float xRaw = Input.GetAxisRaw("Horizontal");
        float xNewVelocity = horizontalSpeed * xRaw;

        //Jumping.
        float yNewVelocity = velocity.y; //If not jumping, keep existing momentum.
        if(jumpEnabled && jumpInput)
        {
            yNewVelocity = jumpSpeed;
            jumping = true;
            jumpInput = false; //Indicate input has been processed.
            jumpEnabled = false;
        }

        //Apply movement.
        velocity = new Vector2(xNewVelocity, yNewVelocity);

        prevGrounded = grounded;
    }

    //Test if the player would collide with the ground or ceiling if moved by the given offset.
    //The parameter determines if the ground or ceiling is being checked.
    //If a collision is detected, return the corresponding RaycastHit. Otherwise, return null.
    private CollisionInfo TestCollidingGround(Vector3 offset, bool ground)
    {
        float yOffset = ground ?
            boxcollider2d.bounds.min.y + offset.y + SURFACE_CHECK_BUFFER :
            boxcollider2d.bounds.max.y + offset.y - SURFACE_CHECK_BUFFER;

        Vector2 leftOrigin = new Vector2(boxcollider2d.bounds.min.x + offset.x + 0.005f, yOffset);
        Vector2 rightOrigin = new Vector2(boxcollider2d.bounds.max.x + offset.x - 0.005f, yOffset);
        Vector2 dir = ground ? Vector2.down : Vector2.up;

        CollisionInfo col = SurfaceRayTest(leftOrigin, dir, GROUND_ANGLE);
        if(col != null) return col;

        col = SurfaceRayTest(rightOrigin, dir, GROUND_ANGLE);
        if(col != null) return col;

        return null;
    }

    //Test if the player would collide with a wall if moved by the given offset.
    //The parameter determines if a right or left wall is being checked.
    //If a collision is detected, return the corresponding RaycastHit. Otherwise, return null.
    private CollisionInfo TestCollidingWall(Vector3 offset, bool right)
    {
        float xOffset = right ? 
            boxcollider2d.bounds.max.x + offset.x - SURFACE_CHECK_BUFFER : 
            boxcollider2d.bounds.min.x + offset.x + SURFACE_CHECK_BUFFER;

        Vector2 topOrigin = new Vector2(xOffset, boxcollider2d.bounds.max.y + offset.y - 0.005f);
        Vector2 middleOrigin = new Vector2(xOffset, boxcollider2d.bounds.center.y + offset.y);
        Vector2 bottomOrigin = new Vector2(xOffset, boxcollider2d.bounds.min.y + offset.y + 0.005f);
        Vector2 dir = right ? Vector2.right : Vector2.left;

        CollisionInfo col = SurfaceRayTest(topOrigin, dir, WALL_ANGLE);
        if(col != null) return col;

        col = SurfaceRayTest(middleOrigin, dir, WALL_ANGLE);
        if(col != null) return col;

        col = SurfaceRayTest(bottomOrigin, dir, WALL_ANGLE);
        if(col != null) return col;

        return null;
    }

    //Helper function that casts a very short ray in a given direction,
    // and determines if a surface of the correct orientation is hit.
    //A hit surface must be angled within the given tolerance to be valid.
    // E.g. if the ray is cast downwards to check for the ground, it can't be too steep,
    // otherwise it wouldn't count as 'ground'.
    private CollisionInfo SurfaceRayTest(Vector2 origin, Vector2 direction, float angleTolerance)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, SURFACE_CHECK_DISTANCE, SURFACE_LAYER_MASK);
        
        //Check if it hit a surface, and if so, that the surface angle is within tolerance.
        //(Ignore hits from colliders inside the ray's origin, as no normal is computed in that instance.)
        if( (hit.collider != null) && 
            (hit.fraction != 0.0f) && 
            (Mathf.Abs(Vector2.Angle(-direction, hit.normal)) <= angleTolerance))
            {
                return new CollisionInfo(hit.point);
            }
            else
            {
                return null;
            }
    }

    //Wait before enabling or disabling the jump.
    private IEnumerator DelayJumpEnabled(bool enabled)
    {
        yield return new WaitForSeconds(JUMP_INPUT_BUFFER_TIME);

        jumpEnabled = enabled;
    }
}
