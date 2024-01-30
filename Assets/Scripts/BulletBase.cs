using UnityEngine;

public abstract class BulletBase : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rbody;
    [SerializeField] private Animator animator;
    
    [SerializeField] private float speed = 4.0f; //Set before Start. Changing later does nothing.
    [SerializeField] private bool shootsRight = true; //If false, shoots to the left.
    [SerializeField] private float lifetime = 3.0f; //Max time bullet can be alive for before despawning.
    [SerializeField] private string targetTag; //Tag of target this bullet hits. ("Player" or "Enemy").

    private float velocity;
    private float aliveTimer = 0.0f; //How long bullet has existed for.
    private bool inFlight = true;

    public void SetSpeed(float speed)
    {
        this.speed = speed;
    }

    public void SetDirection(bool shootingRight)
    {
        shootsRight = shootingRight;
    }

    public abstract void SetDamage(int damage);

    void Start()
    {
        velocity = shootsRight ? speed : -speed;

        //Flip sprite if going left.
        if(!shootsRight)
        {
            Vector3 curScale = transform.localScale;
            transform.localScale = new Vector3(-curScale.x, curScale.y, curScale.z);
        }
    }

    void Update()
    {
        if(inFlight)
        {
            aliveTimer += Time.deltaTime;
        
            if(aliveTimer > lifetime)
            {
                Destroy(gameObject);
            }
            else
            {
                rbody.position += new Vector2(velocity * Time.deltaTime, 0.0f);
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if(other.CompareTag("StaticSolid") || other.CompareTag("MovingSolid") || other.CompareTag(targetTag))
        {
            //Bullet hits this entity, fizzles out and then despawns.
            //Note that the 'hit' animation disables the bullet's collider in the next frame.
            
            inFlight = false;
            animator.SetBool("Hit", true); //Upon completion, the animation will signal back.
        }
    }

    //Called by animator after the fadeout 'hit' animation is complete.
    void FinishFadeout()
    {
        Destroy(gameObject);
    }
}
