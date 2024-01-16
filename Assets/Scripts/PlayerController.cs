using System;
using System.Collections;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    //References.
    [SerializeField] private Animator animator;
    [SerializeField] private MovingEntity entityPhysics;
    [SerializeField] private TrailRenderer dashTrail;
    [SerializeField] private BoxCollider2D hitbox;

    [Serializable]
    private class MovementParameters
    {
        public float runSpeed = 2.5f;
        public float jumpSpeed = 4.0f;
        public float crouchWalkSpeed = 1.0f;
        public float wallSlideSpeed = 0.5f;
        public float wallJumpDuration = 0.15f;
        public float wallJumpHorizontalSpeed = 2.5f;
        public float wallJumpVerticalSpeed = 3.5f;
        public float superJumpSpeed = 6.0f;
        public float superJumpPrepareTime = 0.5f;
        public float superJumpMoveModifier = 0.3f;
        public float dashSpeed = 4.0f;
        public float dashDuration = 0.4f;
        public float dashRepeatDelay = 0.5f;
        public float dashWallJumpHorizSpeed = 3.0f;
        public float dashWallJumpVertSpeed = 4.0f;
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
    private bool wallSliding = false;

    //Surface contacts.
    //These are updated during physics FixedUpdate, so might not be in sync with Update.
    private bool ceilingContact = false;
    private bool leftWallContact = false;
    private bool rightWallContact = false;

    //Launch variables.
    //The player is 'launched' when the environment applies velocity to them.
    private bool horizontalLaunching = false;
    private float HORIZONTAL_LAUNCH_THRESHOLD = 2.0f; //How much imparted speed is needed to register as a launch.
    private float HORIZ_FAST_LAUNCH_THRESHOLD = 4.5f; //How much total speed is needed for the launch to be 'fast'.
    private float VERT_FAST_LAUNCH_THRESHOLD = 6.0f; //How much total speed is needed for the launch to be 'fast'.

    //Miscellaneous movement.
    private bool groundJumpEnabled = false;
    private bool wallJumpEnabled = false;
    private bool wallJumping = false;
    private bool superJumping = false;
    private bool superCrouching = false;
    private bool dashEnabled = true;
    private bool forceCrouch = false;
    private float standingHitboxHeight;

    //Damage.
    private bool damageable = true;

    void Start()
    {
        foreach(InputButton button in Enum.GetValues(typeof(InputButton)))
        {
            StartCoroutine(ProcessButtonInputs(button));
        }

        entityPhysics.EnableFloorMessages();
        entityPhysics.EnableWallMessages();
        entityPhysics.EnableLaunchMessages();

        standingHitboxHeight = hitbox.bounds.size.y;
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

        //Run state machine.
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
        float xNewVelocity = entityPhysics.GetVelocity().x;
        float yNewVelocity = entityPhysics.GetVelocity().y;
        float xRawInput = Input.GetAxisRaw("Horizontal");

        //Crouching.
        if(forceCrouch && !crouching)
        {
            //If the ceiling is too low, try crouching to see if the player now fits inside the gap.
            crouching = true;
        }
        else
        {
            //Check for forced crouching. If inside an area with a low ceiling, the player is forced to crouch.
            //They remain crouched until there's space to stand up again.
            if(crouching)
            {
                //Check for sufficient space above player. (This requires a bit of extra space
                // to avoid being considered squashed again.)
                float requiredSpace = standingHitboxHeight - hitbox.bounds.size.y + 0.02f;
                forceCrouch = entityPhysics.CheckCeilingCollision(requiredSpace);
            }
        
            //Crouch if forced to, or if the input is pressed while grounded.
            crouching = forceCrouch || (Input.GetAxisRaw("Vertical") < 0.0f && !falling && !dashing);
        }

        //Wall sliding. Slide slowly if pressed against a wall while falling.
        wallSliding = falling && yNewVelocity < 0.0f && ((rightWallContact && xRawInput > 0.01f) || (leftWallContact && xRawInput < -0.01f));
        if(wallSliding)
        {
            yNewVelocity = -moveParams.wallSlideSpeed;
            entityPhysics.SetGravityScale(0.0f);
        }
        else
        {
            entityPhysics.SetGravityScale(1.0f);
        }

        //Horizontal movement.
        if(!superCrouching && !wallSliding)
        {
            if(IsInputActive(InputButton.Dash) && !falling && dashEnabled && !forceCrouch)
            {
                //Ground dash in direction we're facing.
                xNewVelocity = transform.localScale.x > 0.0f ? moveParams.dashSpeed : -moveParams.dashSpeed;
                SetInputActive(InputButton.Dash); //Process the input.
                StartCoroutine(HoldDash());
            }
            else if(!wallJumping && !horizontalLaunching)
            {
                float curMoveSpeed;

                if(superJumping)
                {
                    curMoveSpeed = moveParams.runSpeed * moveParams.superJumpMoveModifier;
                }
                else if(falling)
                {
                    //If in the air and moving faster than default, keep that speed.
                    curMoveSpeed = Mathf.Max(Mathf.Abs(xNewVelocity), moveParams.runSpeed);
                }
                else if(crouching)
                {
                    curMoveSpeed = moveParams.crouchWalkSpeed;
                }
                else
                {
                    curMoveSpeed = moveParams.runSpeed;
                }

                //Apply movement direction to speed.
                xNewVelocity = curMoveSpeed * xRawInput;
            }
        }

        //Jumping.
        if(IsInputActive(InputButton.Jump) && !forceCrouch)
        {
            if(groundJumpEnabled)
            {
                if(crouching)
                {
                    //Super jump.
                    SetInputInactive(InputButton.Jump); //Process the input.
                    xNewVelocity = 0.0f;
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
                bool dashHeld = Input.GetAxisRaw("Dash") > 0.1f;
                float xSpeed = dashHeld ? moveParams.dashWallJumpHorizSpeed : moveParams.wallJumpHorizontalSpeed;
                xNewVelocity = rightWallContact ? -xSpeed : xSpeed;
                yNewVelocity = dashHeld ? moveParams.dashWallJumpVertSpeed : moveParams.wallJumpVerticalSpeed;
                SetInputInactive(InputButton.Jump); //Process the input.
                wallJumpEnabled = false;
                StartCoroutine(WallJump(dashHeld));
            }
        }

        //Apply movement.
        entityPhysics.SetVelocity(xNewVelocity, yNewVelocity);
    }

    //While the jump input is held, maintain the jump until released, or the jump is finished.
    private IEnumerator HoldJump()
    {   
        //Wait for a physics tick, to allow the jump to begin.
        yield return new WaitForFixedUpdate();

        while(Input.GetAxisRaw("Jump") > 0.1f)
        {
            //Check if the jump has finished.
            if(ceilingContact || !falling || entityPhysics.GetVelocity().y <= 0.0f)
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
        entityPhysics.SetVelocityY(0.0f);
    }

    //Special jump. The player character stops for a duration to build power,
    // then performs an extra-high jump straight upwards.
    private IEnumerator SuperJump()
    {
        groundJumpEnabled = false;
        superCrouching = true;

        yield return new WaitForSeconds(moveParams.superJumpPrepareTime);

        entityPhysics.SetVelocityY(moveParams.superJumpSpeed);
        jumpTrigger = true;
        superJumping = true;
        superCrouching = false;
        dashTrail.emitting = true;

        //Wait for a physics tick, to allow the jump to begin.
        yield return new WaitForFixedUpdate();

        //Keep horizontal movement disabled during the super jump.
        while(!ceilingContact && falling && entityPhysics.GetVelocity().y > 0.0f)
        {
            yield return null;
        }

        superJumping = false;
        dashTrail.emitting = false;
    }

    //During a wall jump, temporarily disable horizontal input.
    //The player will continue to move with any existing horizontal velocity.
    private IEnumerator WallJump(bool isDashingWallJump)
    {
        wallJumping = true;
        dashTrail.emitting = isDashingWallJump;
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
        dashTrail.emitting = false;
    }

    //While the dash button is held, maintain a dash, until released or the dash is finished.
    private IEnumerator HoldDash()
    {
        float timer = 0.0f;
        dashEnabled = false;
        dashing = true;
        dashTrail.emitting = true;

        //Maintain dash.
        while(Input.GetAxisRaw("Dash") > 0.1f && timer < moveParams.dashDuration)
        {
            //Ground dash in direction we're facing.
            entityPhysics.SetVelocityX(transform.localScale.x > 0.0f ? moveParams.dashSpeed : -moveParams.dashSpeed);

            yield return null;

            timer += Time.deltaTime;
        }

        dashing = false;
        dashTrail.emitting = false;

        //Delay before player can dash again.
        yield return new WaitForSeconds(moveParams.dashRepeatDelay);

        dashEnabled = true;
    }

    private void UpdateAnimations()
    {
        float rawX = Input.GetAxisRaw("Horizontal");
        animator.SetFloat("HorizontalMove", Mathf.Abs(rawX));

        //Determine sprite direction.
        bool changeDirection;
        if(wallJumping)
        {
            float xVelocity = entityPhysics.GetVelocity().x;
            changeDirection =   (xVelocity > 0.0f && transform.localScale.x < 0.0f) ||
                                (xVelocity < 0.0f && transform.localScale.x > 0.0f);
        }
        else
        {
            changeDirection =   (rawX > 0.1f && transform.localScale.x < 0.0f) || 
                                (rawX < -0.1f && transform.localScale.x > 0.0f);
            
        }

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

        animator.SetBool("Dashing", dashing);

        animator.SetBool("WallSliding", wallSliding);
    }

    /**************************
     ***** Message Events *****
     **************************/

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

    //Wait before enabling or disabling the jump.
    private IEnumerator DelayGroundJumpEnabled(bool enabled)
    {
        yield return new WaitForSeconds(INPUT_BUFFER_TIME);

        groundJumpEnabled = enabled;
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

    void OnHorizontalSquash()
    {
        //TODO: Kill player here.
    }

    void OnVerticalSquash()
    {
        if(crouching)
        {
            //TODO: Kill player here.
        }
        else
        {
            //Auto crouch to try and fit inside the smaller space.
            forceCrouch = true;
        }
    }

    void OnLaunch(Vector3 launchVelocity)
    {
        Vector2 totalVelocity = entityPhysics.GetVelocity();

        //Check if there's any imparted speed, and that net velocity isn't zero.
        if(Mathf.Abs(launchVelocity.x) >= HORIZONTAL_LAUNCH_THRESHOLD && Mathf.Abs(totalVelocity.x) > 0.01f)
        {
            StartCoroutine(HorizontalLaunch(totalVelocity.x > HORIZ_FAST_LAUNCH_THRESHOLD, totalVelocity.x > 0.0f));
        }

        //Check if the player was launched upwards, and isn't already considered launched horizontally.
        if(totalVelocity.y >= VERT_FAST_LAUNCH_THRESHOLD && totalVelocity.x < HORIZ_FAST_LAUNCH_THRESHOLD)
        {
            StartCoroutine(VerticalLaunch());
        }
    }

    private IEnumerator HorizontalLaunch(bool isFastLaunch, bool rightward)
    {
        horizontalLaunching = true;
        dashTrail.emitting = isFastLaunch;

        //Wait for a physics tick, to allow the player to register leaving the ground.
        yield return new WaitForFixedUpdate();

        //Player stays launched until they land, hit a wall or manually change direction.
        bool changeDirection = false;
        while(falling && !rightWallContact && !leftWallContact && !changeDirection)
        {
            yield return null;

            changeDirection = rightward ? (Input.GetAxisRaw("Horizontal") < -0.1f) : (Input.GetAxisRaw("Horizontal") > 0.1f);
        }

        horizontalLaunching = false;
        dashTrail.emitting = false;

        //If the launch was aborted due to the player moving in the opposite direction,
        // lose the horizontal speed they had.
        if(changeDirection)
        {
            entityPhysics.SetVelocityX(0.0f);
        }
    }

    private IEnumerator VerticalLaunch()
    {
        dashTrail.emitting = true;

        //Wait for a physics tick, to allow the player to register leaving the ground.
        yield return new WaitForFixedUpdate();

        //Continue emitting trail until upward velocity is lost.
        while(!ceilingContact && falling && entityPhysics.GetVelocity().y > 0.0f)
        {
            yield return null;
        }

        dashTrail.emitting = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if(damageable && other.CompareTag("Enemy"))
        {
            StartCoroutine(TakeDamage(10));
        }
    }

    private IEnumerator TakeDamage(int damage)
    {
        Debug.Log("Hit!");
        damageable = false;
        yield return null;
    }
}
