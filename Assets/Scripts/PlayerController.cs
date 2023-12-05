using System;
using System.Collections;
using System.Collections.Specialized;
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
        public float superJumpSpeed = 6.0f;
        public float superJumpPrepareTime = 0.5f;
        public float superJumpMoveModifier = 0.3f;
        public float dashSpeed = 8.0f;
        public float dashDuration = 0.2f;
        public float dashRepeatDelay = 0.5f;
    }

    [SerializeField]
    private MovementParameters moveParams = new();

    //Inputs.
    private InputButton inputs = 0; //Bitflags. A 'true' flag indicates the input has been pressed and is awaiting a response.
    private const float INPUT_BUFFER_TIME = 0.1f;

    [Flags]
    private enum InputButton
    {
        None = 0,
        Jump = 1 << 0,
        Dash = 1 << 1,
        Fire = 1 << 2
    }

    //Animation signals.
    private bool jumpTrigger = false;
    private bool falling = true; 
    private bool landTrigger = false;
    private bool crouching = false;
    private bool dashing = false;

    //Surface contacts.
    //These are updated during physics FixedUpdate, so might not be in sync with Update.
    private bool ceilingContact = false;
    private bool leftWallContact = false;
    private bool rightWallContact = false;

    //Misc.
    private bool groundJumpEnabled = false;
    private bool wallJumpEnabled = false;
    private bool wallJumping = false;
    private bool superJumping = false;
    private bool superCrouching = false;
    private bool dashEnabled = true;

    void Start()
    {
        foreach(InputButton button in Enum.GetValues(typeof(InputButton)))
        {
            StartCoroutine(ProcessButtonInputs(button));
        }

        platPhysics.EnableFloorMessages();
        platPhysics.EnableWallMessages();
    }

    void Update()
    {
        UpdateMovement();
        UpdateAnimations();
    }

    private void SetInputActive(InputButton button)
    {
        inputs |= button;
    }

    private void SetInputInactive(InputButton button)
    {
        inputs &= ~button;
    }

    private bool IsInputActive(InputButton button)
    {
        return (inputs & button) == button;
    }

    //State machine for processing button inputs.
    private IEnumerator ProcessButtonInputs(InputButton button)
    {
        string inputName;

        //Get input name, while also checking if this is an input we care about.
        switch(button)
        {
        case InputButton.Jump:
            inputName = "Jump";
            break;
        case InputButton.Dash:
            inputName = "Dash";
            break;
        default:
            //Do not process this input.
            yield break;
        }

        while(true)
        {
            //Wait until button is pressed.
            while(Input.GetAxisRaw(inputName) < 0.1f)
            {
                yield return null;
            }

            SetInputActive(button);

            //Buffer input for a brief period. Ignore repeat presses during this time.
            yield return new WaitForSeconds(INPUT_BUFFER_TIME);

            SetInputInactive(button);

            //Wait until button is released.
            while(Input.GetAxisRaw(inputName) > 0.1f)
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
        if(!falling && Input.GetAxisRaw("Vertical") < 0.0f)
        {
            crouching = true;
            xNewVelocity = 0.0f;
            
        }
        else
        {
            crouching = false;
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
        if(IsInputActive(InputButton.Dash) && !falling && dashEnabled)
        {
            //Ground dash in direction we're facing.
            xNewVelocity = transform.localScale.x > 0.0f ? moveParams.dashSpeed : -moveParams.dashSpeed;
            SetInputActive(InputButton.Dash); //Process the input.
            StartCoroutine(HoldDash());
        }
        else if(!wallSliding && !wallJumping && !crouching && !superCrouching)
        {
            xNewVelocity = moveParams.horizontalSpeed * xRawInput;

            if(superJumping)
            {
                xNewVelocity *= moveParams.superJumpMoveModifier;
            }
        }

        //Jumping.
        if(IsInputActive(InputButton.Jump))
        {
            if(groundJumpEnabled)
            {
                if(crouching)
                {
                    //Super jump.
                    SetInputInactive(InputButton.Jump); //Process the input.
                    StartCoroutine(SuperJump());
                }
                else
                {
                    //Normal ground jump.
                    yNewVelocity = moveParams.jumpSpeed;
                    SetInputInactive(InputButton.Jump); //Process the input.
                    jumpTrigger = true;
                    groundJumpEnabled = false;
                    StartCoroutine(HoldJump());
                }
            }
            else if(wallJumpEnabled && falling && !superJumping)
            {
                //Wall jump.
                xNewVelocity = rightWallContact ? -moveParams.wallJumpHorizontalSpeed : moveParams.wallJumpHorizontalSpeed;
                yNewVelocity = moveParams.wallJumpVerticalSpeed;
                SetInputInactive(InputButton.Jump); //Process the input.
                wallJumpEnabled = false;
                StartCoroutine(WallJump());
            }
        }

        //Apply movement.
        platPhysics.SetVelocity(xNewVelocity, yNewVelocity);
    }

    //While the jump input is held, maintain the jump until released, or the jump is finished.
    private IEnumerator HoldJump()
    {   
        //Wait for a physics tick, to allow the jump to begin.
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

    //Special jump. The player character stops for a duration to build power,
    // then performs an extra-high jump straight upwards.
    private IEnumerator SuperJump()
    {
        groundJumpEnabled = false;
        superCrouching = true;

        yield return new WaitForSeconds(moveParams.superJumpPrepareTime);

        platPhysics.SetVelocityY(moveParams.superJumpSpeed);
        jumpTrigger = true;
        superJumping = true;
        superCrouching = false;

        //Wait for a physics tick, to allow the jump to begin.
        yield return new WaitForFixedUpdate();

        //Keep horizontal movement disabled during the super jump.
        while(!ceilingContact && falling && platPhysics.GetVelocity().y > 0.0f)
        {
            yield return null;
        }

        superJumping = false;
    }

    //During a wall jump, temporarily disable horizontal input.
    //The player will continue to move with any existing horizontal velocity.
    private IEnumerator WallJump()
    {
        wallJumping = true;
        double curTime = Time.realtimeSinceStartupAsDouble;

        //Wait for a physics tick, to allow the jump to begin.
        yield return new WaitForFixedUpdate();

        //Start timer accounting for the fixed update wait.
        float timer = (float)(Time.realtimeSinceStartupAsDouble - curTime);

        //Wait until wall jump is finished.
        while(!rightWallContact && !leftWallContact && timer < moveParams.wallJumpDuration)
        {
            yield return null;

            timer += Time.deltaTime;
        }

        wallJumping = false;
    }

    //While the dash button is held, maintain a dash, until released or the dash is finished.
    private IEnumerator HoldDash()
    {
        float timer = 0.0f;
        dashEnabled = false;
        dashing = true;

        //Maintain dash.
        while(Input.GetAxisRaw("Dash") > 0.1f && timer < moveParams.dashDuration)
        {
            //Ground dash in direction we're facing.
            platPhysics.SetVelocityX(transform.localScale.x > 0.0f ? moveParams.dashSpeed : -moveParams.dashSpeed);

            yield return null;

            timer += Time.deltaTime;
        }

        dashing = false;

        //Delay before player can dash again.
        yield return new WaitForSeconds(moveParams.dashRepeatDelay);

        dashEnabled = true;
    }

    private void UpdateAnimations()
    {
        float rawX = Input.GetAxisRaw("Horizontal");
        animator.SetFloat("HorizontalMove", Mathf.Abs(rawX));

        //Flip the sprite if moving the other direction.
        bool changeDirection = 
                (rawX > 0.1f && transform.localScale.x < 0.0f) || 
                (rawX < -0.1f && transform.localScale.x > 0.0f);
        if(changeDirection && !superCrouching)
        {
            Vector3 curScale = transform.localScale;
            transform.localScale = new Vector3(-curScale.x, curScale.y, curScale.z);
        }

        animator.SetBool("Jumping", jumpTrigger);
        jumpTrigger = false; //Single trigger.

        animator.SetBool("Falling", falling);

        animator.SetBool("Landing", landTrigger);
        landTrigger = false; //Single trigger.

        animator.SetBool("Crouching", crouching);

        animator.SetBool("PrepareSuperJump", superCrouching);
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
            landTrigger = true;

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
        yield return new WaitForSeconds(INPUT_BUFFER_TIME);

        groundJumpEnabled = enabled;
    }
}
