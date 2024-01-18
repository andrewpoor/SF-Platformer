using UnityEngine;
using UnityEngine.Assertions;

//Component for anything that can damage a player, including enemies, projectiles
// and environmental damage like spikes.
public class PlayerDamager : MonoBehaviour
{
    [SerializeField] private int damage = 20;
    [SerializeField] private bool isLethal = false; //If true, kills player in one hit regardless of damage.

    public int GetDamage()
    {
        Assert.IsTrue(damage > 0);
        return damage;
    }

    public bool IsLethal()
    {
        return isLethal;
    }
}
