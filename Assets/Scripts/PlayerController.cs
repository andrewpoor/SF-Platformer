using System.Collections;
using System.Collections.Generic;
//using System.Numerics;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float horizontalSpeed = 2.0f;
    public float jumpHeight = 2.0f;

    public Rigidbody2D rigidbody2d;
    public BoxCollider2D boxcollider2d;
    public Animator animator;

    private bool grounded = false; //True if the player is on the ground.
    private bool prevGrounded = false; //Value of grounded variable in previous frame.
    private bool jumping = false; //Signals the animator to jump.
    private bool jumpEnabled = false;
    private float jumpForce;

    private bool jumpInput = false; //True indicates a jump input is waiting to be processed.
    private const float JUMP_INPUT_BUFFER_TIME = 0.15f;

    private const float SURFACE_CHECK_BUFFER = 0.01f; //Surface check raycasts should start inset from the bounds of the collider.
    private const float SURFACE_CHECK_DISTANCE = 0.04f + SURFACE_CHECK_BUFFER; //How close a surface must be to be considered in contact with the player.
    private const int SURFACE_LAYER_MASK = 1 << 3;
    private const float GROUND_ANGLE = 64.0f; //How steep a surface can be to be considered ground.
    private const float WALL_ANGLE = 25.0f; //How slanted a surface can be to be considered a wall.

    private enum CollisionSurface
    {
        Ground,
        RightWall,
        LeftWall,
        Ceiling
    }

    void Start()
    {
        //Calculate force based on desired jump height.
        jumpForce = Mathf.Sqrt(-2 * Physics2D.gravity.y * rigidbody2d.gravityScale * jumpHeight);

        StartCoroutine(ProcessJumpInputs());
    }

    void Update()
    {
        UpdateAnimations();
    }

    //State machine for processing jump inputs.
    private IEnumerator ProcessJumpInputs()
    {
        while(true)
        {
            //Wait until jump is pressed.
            while(Input.GetAxisRaw("Jump") < 0.1f)
            //while(false)
            {
                yield return null;
            }

            jumpInput = true;

            //Buffer input for a brief period. Ignore repeat presses during this time.
            yield return new WaitForSeconds(JUMP_INPUT_BUFFER_TIME);

            jumpInput = false;

            //Wait until jump is released.
            while(Input.GetAxisRaw("Jump") > 0.1f)
            //while(false)
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

    void FixedUpdate()
    {
        grounded = TestCollidingGround();

        //Check if the player just landed.
        if(!prevGrounded && grounded)
        {
            //Wait before enabling the jump. This adds a brief delay, to allow
            // collision physics to act first.
            StartCoroutine(DelayJumpEnabled(true));
        }

        //Check if the player just left the ground.
        if(prevGrounded && !grounded)
        {
            //Wait before disabling the jump. This provides a small window
            // during which the player can still jump after falling off an edge.
            StartCoroutine(DelayJumpEnabled(false));
        }

        //Increase gravity while grounded to make the player stick to the floor.
        rigidbody2d.gravityScale = grounded ? 5 : 1;

        bool rightWall = TestCollidingWall(true);
        bool leftWall = TestCollidingWall(false);

        //Horizontal movement. Don't move if pressing against a wall.
        float xVelocity = 0.0f;
        float yVelocity = rigidbody2d.velocity.y;
        float xRaw = Input.GetAxisRaw("Horizontal");
        if((xRaw > 0.0f && !rightWall) || (xRaw < 0.0f && !leftWall))
        {
            xVelocity = horizontalSpeed * xRaw;
        }

        rigidbody2d.velocity = new Vector2(xVelocity, yVelocity);

        //Jumping.
        if(jumpEnabled && jumpInput)
        {
            rigidbody2d.gravityScale = 1;
            rigidbody2d.AddForce(new Vector2(0.0f, jumpForce), ForceMode2D.Impulse);
            jumping = true;
            jumpInput = false; //Indicate input has been processed.
            jumpEnabled = false;
        }

        prevGrounded = grounded;
    }

    //Test if the player is currently colliding with the ground.
    private bool TestCollidingGround()
    {
        float yOffset = boxcollider2d.bounds.min.y - SURFACE_CHECK_BUFFER;
        Vector2 bottomLeft = new Vector2(boxcollider2d.bounds.min.x, yOffset);
        Vector2 bottomRight = new Vector2(boxcollider2d.bounds.max.x, yOffset);

        bool isGround = SurfaceRayTest(bottomLeft, Vector2.down, GROUND_ANGLE);
        isGround |= SurfaceRayTest(bottomRight, Vector2.down, GROUND_ANGLE);

        return isGround;
    }

    //Test if the player is currently colliding with a wall.
    //The parameter determines if a right or left wall is being checked.
    private bool TestCollidingWall(bool right)
    {
        float xOffset = right ? 
            boxcollider2d.bounds.max.x - SURFACE_CHECK_BUFFER : 
            boxcollider2d.bounds.min.x + SURFACE_CHECK_BUFFER;

        Vector2 topOrigin = new Vector2(xOffset, boxcollider2d.bounds.max.y);
        Vector2 bottomOrigin = new Vector2(xOffset, boxcollider2d.bounds.min.y);
        Vector2 dir = right ? Vector2.right : Vector2.left;

        bool isWall = SurfaceRayTest(topOrigin, dir, WALL_ANGLE);
        isWall |= SurfaceRayTest(bottomOrigin, dir, WALL_ANGLE);

        return isWall;
    }

    //Helper function that casts a very short ray in a given direction,
    // and determines if a surface of the correct orientation is hit.
    //A hit surface must be angled within the given tolerance to be valid.
    // E.g. if the ray is cast downwards to check for the ground, it can't be too steep,
    // otherwise it wouldn't count as 'ground'.
    private bool SurfaceRayTest(Vector2 origin, Vector2 direction, float angleTolerance)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, SURFACE_CHECK_DISTANCE, SURFACE_LAYER_MASK);

        if(hit.collider)
        {
            Debug.DrawLine(origin, hit.point);
        }
        
        //Check if it hit a surface, and if so, that the surface angle is within tolerance.
        //(Ignore hits from colliders inside the ray's origin, as no normal is computed in that instance.)
        return (hit.collider != null) && 
            (hit.fraction != 0.0f) && 
            (Mathf.Abs(Vector2.Angle(-direction, hit.normal)) <= angleTolerance);
    }

    //Wait before enabling or disabling the jump.
    private IEnumerator DelayJumpEnabled(bool enabled)
    {
        yield return new WaitForSeconds(JUMP_INPUT_BUFFER_TIME);

        jumpEnabled = enabled;
    }
}
