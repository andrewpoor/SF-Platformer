using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float horizontalSpeed = 2.0f;
    public float jumpHeight = 2.0f;

    public Rigidbody2D rigidbody2d;
    public Animator animator;

    private bool grounded = false; //True if the player is on the ground.
    private bool jumping = false; //Signals the animator to jump.
    private bool jumpEnabled = false;
    private float jumpForce;

    private bool jumpInput = false; //True indicates a jump input is waiting to be processed.
    private const float JUMP_INPUT_BUFFER_TIME = 0.15f;

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
        //Horizontal movement.
        float xVelocity = horizontalSpeed * Input.GetAxisRaw("Horizontal");
        float yVelocity = rigidbody2d.velocity.y;
        rigidbody2d.velocity = new Vector2(xVelocity, yVelocity);

        //Vector3 movement = new Vector3(xMove, 0.0f, 0.0f);
        //transform.Translate(movement);
        //rigidbody2d.MovePosition(transform.position + movement);
        //rigidbody2d.AddForce(new Vector2(xMove, 0.0f), ForceMode2D.Impulse);
        
        /*float xRaw = Input.GetAxisRaw("Horizontal");
        if(Mathf.Abs(xRaw) > 0.1)
        {
            if(Mathf.Abs(rigidbody2d.velocity.x) < horizontalSpeed)
            {
                rigidbody2d.AddForce(new Vector2(xRaw, 0.0f), ForceMode2D.Impulse);
            }
        }*/

        //Jumping.
        if(jumpEnabled && jumpInput)
        {
            rigidbody2d.AddForce(new Vector2(0.0f, jumpForce), ForceMode2D.Impulse);
            jumping = true;
            jumpInput = false; //Indicate input has been processed.
            jumpEnabled = false;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        grounded = true;

        //Wait before enabling the jump. This adds a brief delay, to allow
        // collision physics to act first.
        StartCoroutine(DelayJumpEnabled(true));
    }

    void OnTriggerExit2D(Collider2D other)
    {
        grounded = false;

        //Wait before disabling the jump. This provides a small window
        // during which the player can still jump after falling off an edge.
        StartCoroutine(DelayJumpEnabled(false));
    }

    //Wait before enabling or disabling the jump.
    private IEnumerator DelayJumpEnabled(bool enabled)
    {
        yield return new WaitForSeconds(JUMP_INPUT_BUFFER_TIME);

        jumpEnabled = enabled;
    }
}
