using UnityEngine;

public class PlayerBullet : MonoBehaviour
{
    [SerializeField] private EnemyDamager enemyDamager;
    
    [SerializeField] private float speed = 5.0f; //Set before Start. Changing later does nothing.
    [SerializeField] private bool shootsRight = true; //If false, shoots to the left.
    [SerializeField] private float lifetime = 3.0f; //Max time bullet can be alive for before despawning.

    private float velocity;
    private float aliveTimer = 0.0f; //How long bullet has existed for.

    public void SetSpeed(float speed)
    {
        this.speed = speed;
    }

    public void SetDirection(bool shootingRight)
    {
        shootsRight = shootingRight;
    }

    public void SetDamage(int damage)
    {
        enemyDamager.SetDamage(damage);
    }

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
        aliveTimer += Time.deltaTime;
        
        if(aliveTimer > lifetime)
        {
            Destroy(gameObject);
        }
        else
        {
            transform.position += new Vector3(velocity * Time.deltaTime, 0.0f, 0.0f);
        }
    }
}
