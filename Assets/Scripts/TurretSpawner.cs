using UnityEngine;

public class TurretSpawner : EnemySpawnerBase
{
    [SerializeField] private Enemy_Turret turretPrefab;

    //Enemy parameters.
    [SerializeField] private float shootRepeatDelay = 2.0f;

    protected override GameObject SpawnEnemy()
    {
        Enemy_Turret enemy = Instantiate(turretPrefab, transform.position, transform.rotation);
        enemy.Initialise(shootRepeatDelay);

        return enemy.gameObject;
    }
}
