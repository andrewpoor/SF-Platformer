using UnityEngine;

public class EnemySpawnerDrone : EnemySpawnerBase
{
    [SerializeField] private EnemyDrone dronePrefab;

    //Enemy parameters.
    [SerializeField] private float patrolDistance = 2.0f;
    [SerializeField] private float moveSpeed = 1.0f;
    [SerializeField] private bool moveLeft = true;

    protected override GameObject SpawnEnemy()
    {
        EnemyDrone enemy = Instantiate(dronePrefab, transform.position, transform.rotation);
        enemy.Initialise(patrolDistance, moveSpeed, moveLeft);

        return enemy.gameObject;
    }
}
