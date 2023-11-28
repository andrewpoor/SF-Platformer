using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    //References.
    public Animator animator;
    public PlatformerPhysics platPhysics;

    [System.Serializable]
    private class MovementParameters
    {
        public float horizontalSpeed = 2.5f;
        public float jumpSpeed = 4.0f;
        public float wallSlideSpeed = 0.5f;
        public float wallJumpDuration = 0.15f;
        public float wallJumpVerticalSpeed = 3.5f;
        public float wallJumpHorizontalSpeed = 2.5f;
    }

    [SerializeField]
    private MovementParameters moveParams = new();

    //Inputs.
    private bool jumpInput = false; //True indicates a jump input is waiting to be processed.
    private const float JUMP_INPUT_BUFFER_TIME = 0.1f;

    //Animation signals.
    private bool jumping = false;
    private bool falling = true; 
    private bool landing = false;
    private bool crouching = false;

    //Misc.
    private bool groundJumpEnabled = false;
    private bool wallJumpEnabled = false;
    private bool horizontalMoveEnabled = true;
    private bool ceilingContact = false;
    private bool leftWallContact = false;
    private bool rightWallContact = false;

    void Start()
    {
        StartCoroutine(ProcessJumpInputs());

        platPhysics.EnableFloorMessages();
        platPhysics.EnableWallMessages();
    }

    void Update()
    {
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

    //Respond to user input and determine velocity for the next frame.
    private void UpdateMovement()
    {
        float xNewVelocity = platPhysics.GetVelocity().x;
        float yNewVelocity = platPhysics.GetVelocity().y;
        float xRawInput = Input.GetAxisRaw("Horizontal");

        //Crouching. Only crouch when grounded.
        if(!falling)
        {
            if(Input.GetAxisRaw("Vertical") < 0.0f)
            {
                //Crouch.
                crouching = true;
                horizontalMoveEnabled = false;
                xNewVelocity = 0.0f;
            }
            else
            {
                //Uncrouch.
                crouching = false;
                horizontalMoveEnabled = true;
            }
        }

        //Wall sliding. Slide slowly if pressed against a wall while falling.
        bool wallSliding = falling && yNewVelocity < 0.0f && ((rightWallContact && xRawInput > 0.01f) || (leftWallContact && xRawInput < -0.01f));
        if(wallSliding)
        {
            xNewVelocity = 0.0f;
            yNewVelocity = -moveParams.wallSlideSpeed;
            platPhysics.SetGravityScale(0.0f);
        }
        else
        {
            platPhysics.SetGravityScale(1.0f);
        }

        //Horizontal movement.
        if(horizontalMoveEnabled && !wallSliding)
        {
            xNewVelocity = moveParams.horizontalSpeed * xRawInput;
        }

        //Jumping.
        if(groundJumpEnabled && jumpInput)
        {
            yNewVelocity = moveParams.jumpSpeed;
            jumpInput = false; //Process the input.
            jumping = true;
            groundJumpEnabled = false;
            StartCoroutine(HoldJump());
        }
        else if(wallJumpEnabled && jumpInput)
        {
            xNewVelocity = rightWallContact ? -moveParams.wallJumpHorizontalSpeed : moveParams.wallJumpHorizontalSpeed;
            yNewVelocity = moveParams.wallJumpVerticalSpeed;
            jumpInput = false; //Process the input.
            wallJumpEnabled = false;
            StartCoroutine(TempDisableHorizontalInput());
        }

        //Apply movement.
        platPhysics.SetVelocity(xNewVelocity, yNewVelocity);
    }

    //While the jump input is held, maintain the jump until released, or the jump is finished.
    private IEnumerator HoldJump()
    {   
        //First wait for a physics tick, to allow the jump to begin.
        yield return new WaitForFixedUpdate();

        while(Input.GetAxisRaw("Jump") > 0.1f)
        {
            //Check if the jump has finished.
            if(ceilingContact || !falling || platPhysics.GetVelocity().y <= 0.0f)
            {
                //Jump finished naturally, so no need to do anything else.
                yield break;
            }
            else
            {
                yield return null;
            }
        }

        //Abort jump if input is released before the jump is finished.
        platPhysics.SetVelocityY(0.0f);
    }

    //Temporarily disable horizontal input. The player will continue to move with
    // any existing horizontal velocity.
    private IEnumerator TempDisableHorizontalInput()
    {
        horizontalMoveEnabled = false;

        yield return new WaitForSeconds(moveParams.wallJumpDuration);

        horizontalMoveEnabled = true;
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
        jumping = false; //Single trigger.

        animator.SetBool("Falling", falling);

        animator.SetBool("Landing", landing);
        landing = false; //Single trigger.

        animator.SetBool("Crouching", crouching);
    }

    void OnLeaveFloor(bool isCeiling)
    {
        if(isCeiling)
        {
            ceilingContact = false;
        }
        else //Left the ground.
        {
            falling = true;

            //Wait before disabling the jump. This provides a small window
            // during which the player can still jump after falling off an edge.
            StartCoroutine(DelayGroundJumpEnabled(false));
        }
    }

    void OnTouchFloor(bool isCeiling)
    {
        if(isCeiling)
        {
            ceilingContact = true;
        }
        else //Landed on the ground.
        {
            falling = false;
            landing = true;

            //Wait before enabling the jump. This adds a brief delay, to allow
            // collision physics to act first.
            StartCoroutine(DelayGroundJumpEnabled(true));
        }
    }

    void OnLeaveWall(bool isRightWall)
    {
        if(isRightWall)
        {
            rightWallContact = false;
        }
        else
        {
            leftWallContact = false;
        }

        wallJumpEnabled = false;
    }

    void OnTouchWall(bool isRightWall)
    {
        if(isRightWall)
        {
            rightWallContact = true;
        }
        else
        {
            leftWallContact = true;
        }

        wallJumpEnabled = true;
    }

    //Wait before enabling or disabling the jump.
    private IEnumerator DelayGroundJumpEnabled(bool enabled)
    {
        yield return new WaitForSeconds(JUMP_INPUT_BUFFER_TIME);

        groundJumpEnabled = enabled;
    }
}
