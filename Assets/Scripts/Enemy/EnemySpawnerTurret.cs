using UnityEngine;

public class EnemySpawnerTurret : EnemySpawnerBase
{
    [SerializeField] private EnemyTurret turretPrefab;

    //Enemy parameters.
    [SerializeField] private float shootRepeatDelay = 2.0f;

    protected override GameObject SpawnEnemy()
    {
        EnemyTurret enemy = Instantiate(turretPrefab, transform.position, transform.rotation);
        enemy.Initialise(shootRepeatDelay);

        return enemy.gameObject;
    }
}
