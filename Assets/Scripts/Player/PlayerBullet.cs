using UnityEngine;

public class PlayerBullet : BulletBase
{
    [SerializeField] private EnemyDamager enemyDamager;

    public override void SetDamage(int damage)
    {
        enemyDamager.SetDamage(damage);
    }
}
