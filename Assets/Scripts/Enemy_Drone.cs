using System.Collections;
using UnityEngine;

public class Enemy_Drone : MonoBehaviour
{
    //Component references.
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rbody;

    //Prefab references.
    [SerializeField] private EnemyExplosion explosionPrefab;

    //Parameters.
    [SerializeField] private float patrolDistance = 2.0f;
    [SerializeField] private float moveSpeed = 1.0f;
    [SerializeField] private bool moveLeft = true;

    private bool turning = false;
    private bool alive = true;

    void Start()
    {
        //Start facing the direction it needs to move in. Default is facing left.
        if(!moveLeft)
        {
            ChangeDirection();
        }

        StartCoroutine(Patrol());
    }

    void Update()
    {
        
    }

    //Movement pattern for this enemy.
    private IEnumerator Patrol()
    {
        while(alive)
        {
            animator.SetBool("Turning", false); //Play move animation.

            float distanceTravelled = 0.0f;

            //Move in the facing direction until full distance is reached.
            while(distanceTravelled < patrolDistance)
            {
                float frameMovement = moveSpeed * Time.deltaTime;
                float remainingDistance = patrolDistance - distanceTravelled;

                if(remainingDistance < frameMovement)
                {
                    //Set movement so the drone moves exactly to reach the full distance.
                    frameMovement = remainingDistance;
                    distanceTravelled = patrolDistance;
                }
                else
                {
                    distanceTravelled += frameMovement;
                }

                rbody.position += new Vector2(moveLeft ? -frameMovement : frameMovement, 0.0f);

                yield return null;
            }
            
            //Turn around.
            animator.SetBool("Turning", true); //Play turning animation.
            turning = true;
            moveLeft = !moveLeft;

            //Wait until animation event indicating the drone has finished turning around.
            while(turning)
            {
                yield return null;
            }
        }
    }

    //Change direction drone is facing. Called by an animation event.
    void ChangeDirection()
    {
        Vector3 curScale = transform.localScale;
        transform.localScale = new Vector3(-curScale.x, curScale.y, curScale.z);
        turning = false;
    }

    //Message event for responding to taking damage.
    void OnTakeDamage(int damage)
    {}

    //Message event for responding to signal to be killed.
    void OnKilled()
    {
        Instantiate(explosionPrefab, transform.position, transform.rotation);
        Destroy(gameObject);
    }
}
