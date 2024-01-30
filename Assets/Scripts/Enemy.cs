using UnityEngine;

public class Enemy : MonoBehaviour
{
    //Component references.
    [SerializeField] private SpriteRenderer spriteRenderer;

    [SerializeField] private float maxHealth = 60;

    private float health;
    private readonly Color hitColor = new Color(1.0f, 0.7f, 0.9f);
    private const float HIT_COLOR_DURATION = 0.15f;
    private float hitColorTimer = HIT_COLOR_DURATION;

    void Start()
    {
        health = maxHealth;
    }

    void Update()
    {
        //Play hit color effect when damaged.
        if(hitColorTimer < HIT_COLOR_DURATION)
        {
            hitColorTimer += Time.deltaTime;
            spriteRenderer.color = hitColor;
        }
        else
        {
            spriteRenderer.color = Color.white;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        //Check if collided object is something that can damage the enemy.
        EnemyDamager damager = other.GetComponent<EnemyDamager>();

        if(damager != null)
        {
            int damage = damager.GetDamage();
            health -= damage;

            if(health > 0)
            {
                gameObject.SendMessage("OnTakeDamage", damage); //Individual enemy scripts should implement this.
                hitColorTimer = 0.0f; //Start hit colour effect.
            }
            else
            {
                gameObject.SendMessage("OnKilled"); //Individual enemy scripts should implement this.
            }
            
        }
    }
}
