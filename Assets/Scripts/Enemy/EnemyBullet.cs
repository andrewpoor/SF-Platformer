using UnityEngine;

public class EnemyBullet : BulletBase
{
    [SerializeField] private PlayerDamager playerDamager;

    public override void SetDamage(int damage)
    {
        playerDamager.SetDamage(damage);
    }
}