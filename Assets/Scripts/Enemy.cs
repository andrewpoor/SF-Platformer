using UnityEngine;

public class Enemy : MonoBehaviour
{
    [SerializeField] private float maxHealth = 60;

    private float health;

    void Start()
    {
        health = maxHealth;
    }

    void Update()
    {
        
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
            }
            else
            {
                gameObject.SendMessage("OnKilled"); //Individual enemy scripts should implement this.
            }
            
        }
    }
}
