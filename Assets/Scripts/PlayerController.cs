using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float horizontalSpeed = 2.5f;
    public float jumpSpeed = 4.0f;

    public Animator animator;
    public PlatformerPhysics platPhysics;

    //Inputs
    private bool jumpInput = false; //True indicates a jump input is waiting to be processed.
    private const float JUMP_INPUT_BUFFER_TIME = 0.15f;

    //Animation signals
    private bool jumping = false;
    private bool falling = false; 
    private bool landing = false;

    private bool jumpEnabled = false;

    void Start()
    {
        StartCoroutine(ProcessJumpInputs());
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

    //Respond to user input and react to physics calculations.
    private void UpdateMovement()
    {
        //Horizontal movement. Only move when input is pressed.
        float xRaw = Input.GetAxisRaw("Horizontal");
        float xNewVelocity = horizontalSpeed * xRaw;

        //Vertical movement. Jumping or slopes.
        float yNewVelocity = platPhysics.GetVelocity().y; //By default, keep existing vertical momentum.
        if(jumpEnabled && jumpInput)
        {
            yNewVelocity = jumpSpeed;
            jumping = true;
            jumpInput = false; //Indicate input has been processed.
            jumpEnabled = false;
        }

        //Apply movement.
        platPhysics.SetVelocity(xNewVelocity, yNewVelocity);
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

    }

    public void OnLeaveGround()
    {
        falling = true;

        //Wait before disabling the jump. This provides a small window
        // during which the player can still jump after falling off an edge.
        StartCoroutine(DelayJumpEnabled(false));
    }

    public void OnLanded()
    {
        falling = false;
        landing = true;

        //Wait before enabling the jump. This adds a brief delay, to allow
        // collision physics to act first.
        StartCoroutine(DelayJumpEnabled(true));
    }

    //Wait before enabling or disabling the jump.
    private IEnumerator DelayJumpEnabled(bool enabled)
    {
        yield return new WaitForSeconds(JUMP_INPUT_BUFFER_TIME);

        jumpEnabled = enabled;
    }
}
