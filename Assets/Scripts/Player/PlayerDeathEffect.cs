using UnityEngine;

public class PlayerDeathEffect : MonoBehaviour
{
    //Function called when the animation has finished.
    //Can react to the player's death here.
    void FinishEffect()
    {
        Destroy(gameObject);
    }
}
