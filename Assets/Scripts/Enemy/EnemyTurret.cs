using UnityEngine;

public class EnemyTurret : MonoBehaviour
{
    //Component references.
    [SerializeField] private Animator animator;
    [SerializeField] private Transform bulletSpawnPos;

    //Prefab references.
    [SerializeField] private EnemyBullet bulletPrefab;
    [SerializeField] private EnemyExplosion explosionPrefab;

    //Parameters.
    [SerializeField] private float shootRepeatDelay = 2.0f;
    [SerializeField] private float delayBeforeFirstShot = 0.5f;
    [SerializeField] private float playerDistanceThreshold = 3.5f; //Threshold before turret starts shooting at player.

    private Transform player;

    private bool facingRight = false;
    private bool turning = false;
    private bool turretActive = false; //Won't shoot until it's seen the player (i.e. turret is on-screen).
    private float shootTimer;

    void Start()
    {
        player = GameObject.FindWithTag("Player").transform;

        //Once active, the turret will wait a short moment before it begins firing.
        shootTimer = shootRepeatDelay - delayBeforeFirstShot;
    }

    public void Initialise(float shootRepeatDelay)
    {
        this.shootRepeatDelay = shootRepeatDelay;
    }

    void Update()
    {
        if(!turning && player != null)
        {
            float distToPlayer = player.position.x - transform.position.x;

            //The turret 'activates' once the player is close enough, then stays active even if the
            // player moves further away afterwards.
            turretActive = turretActive || (Mathf.Abs(distToPlayer) <= playerDistanceThreshold);

            //Check which side player is on, and begin turning if needed.
            if((facingRight && distToPlayer < 0.0f) || (!facingRight && distToPlayer > 0.0f))
            {
                //Turn to face player.
                animator.SetBool("Turning", true); //Play turning animation.
                turning = true;
                facingRight = !facingRight;
                shootTimer = 0.0f; //Reset shoot timer. Must start over any time it turns.
            }
            else if(turretActive)
            {
                //Shoot bullets periodically.
                shootTimer += Time.deltaTime;
                if(shootTimer > shootRepeatDelay)
                {
                    //Spawn and fire a bullet.
                    EnemyBullet bullet = Instantiate(bulletPrefab, bulletSpawnPos.position, transform.rotation);
                    bullet.SetDirection(facingRight);

                    shootTimer = 0.0f;
                }
            }
        }
    }

    //Change direction turret is facing. Called by an animation event.
    void ChangeDirection()
    {
        Vector3 curScale = transform.localScale;
        transform.localScale = new Vector3(-curScale.x, curScale.y, curScale.z);
        turning = false;
        animator.SetBool("Turning", false);
    }

    //Message event for responding to taking damage.
    void OnTakeDamage(int damage)
    {}

    //Message event for responding to signal to be killed.
    void OnKilled()
    {
        Instantiate(explosionPrefab, transform.position, transform.rotation);
        Destroy(gameObject);
    }
}
