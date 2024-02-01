using UnityEngine;

public abstract class EnemySpawnerBase : MonoBehaviour
{
    protected Enemy spawnedEnemy;

    private bool withinPlayerRange;

    void Start()
    {
        //Renderer is only for the editor, so disable it during play.
        GetComponent<SpriteRenderer>().enabled = false;
    }

    public bool IsWithinPlayerRange()
    {
        return withinPlayerRange;
    }

    //When camera trigger overlaps, this indicates the player has drawn close,
    // so an enemy should be spawned.
    void OnTriggerEnter2D(Collider2D other)
    {
        //Only spawn the enemy if it's not already spawned.
        //Some enemies move, so might still be on-screen even when the spawner wasn't and just got triggered.
        if(other.CompareTag("MainCamera"))
        {
            withinPlayerRange = true;

            if(spawnedEnemy == null)
            {
                spawnedEnemy = SpawnEnemy().GetComponent<Enemy>();
                spawnedEnemy.RegisterSpawner(this);
            }
        }
    }

    //When camera trigger moves far enough from the summoned entity and its spawner, the entity despawns.
    void OnTriggerExit2D(Collider2D other)
    {
        if(other.CompareTag("MainCamera"))
        {
            //Check if spawned enemy is still on-screen or not.
            if(spawnedEnemy != null && !spawnedEnemy.IsWithinPlayerRange())
            {
                //Only despawn if both the summoned entity and its spawner are off-screen.
                Destroy(spawnedEnemy.gameObject);
            }
            else
            {
                withinPlayerRange = false;
            }
        }
    }

    protected abstract GameObject SpawnEnemy();
}
