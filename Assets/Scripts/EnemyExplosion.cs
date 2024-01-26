using UnityEngine;

public class EnemyExplosion : MonoBehaviour
{
    //Function called when the animation has finished.
    //Can react to the enemy's death here.
    void FinishEffect()
    {
        Destroy(gameObject);
    }
}
