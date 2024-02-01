using UnityEngine;
using UnityEngine.Assertions;

public class EnemyDamager : MonoBehaviour
{
    [SerializeField] private int damage = 20;

    public int GetDamage()
    {
        Assert.IsTrue(damage > 0);
        return damage;
    }

    public void SetDamage(int damage)
    {
        this.damage = damage;
    }
}
