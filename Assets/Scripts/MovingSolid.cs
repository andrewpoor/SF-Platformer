using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MovingSolid : MonoBehaviour
{
    [SerializeField] private BoxCollider2D hitbox;
    [SerializeField] private Rigidbody2D rBody;

    private const float MOVE_UNIT = 0.01f;
    private const int ENTITY_LAYER_MASK = 1 << 6;

    //Size of box used to detect overlaps. Cached as hitbox is disabled during movement.
    private Vector2 overlapBoxSize;

    void Start()
    {
        overlapBoxSize = hitbox.bounds.size;
    }

    //Move the solid the given amount. This will push or carry other entities,
    // but won't interact with other solids.
    //Should be called from FixedUpdate.
    public void Move(float xMove, float yMove)
    {
        float curRotation = transform.rotation.eulerAngles.z;
        float fps = 1.0f / Time.fixedDeltaTime;
        Vector2 curVelocity = new(xMove * fps, yMove * fps);

        List<MovingEntity> ridingEntities = FindAllRidingEntities(curVelocity);

        //Disable hitbox to avoid extra collisions when riding actors are moved.
        hitbox.enabled = false;

        bool rightward = xMove > 0.0f;
        bool upward = yMove > 0.0f;
        float xAmount = Mathf.Abs(xMove);
        float yAmount = Mathf.Abs(yMove);
        
        //Move one unit at a time, checking collisions along the way.
        while(xAmount > 0.0f || yAmount > 0.0f)
        {
            if(xAmount > 0.0f)
            {
                //Determine movement amount for this step.
                float xSmallMove = xAmount > MOVE_UNIT ? MOVE_UNIT : xAmount;
                xAmount -= xSmallMove;

                //Simulate movement, to check for entities that should be pushed.
                RaycastHit2D[] hits = Physics2D.BoxCastAll(rBody.position, overlapBoxSize, curRotation, 
                        rightward ? Vector2.right : Vector2.left, xSmallMove, ENTITY_LAYER_MASK);

                //Push any entities that are in the way.
                List<MovingEntity> pushedEntities = new();
                foreach(RaycastHit2D hit in hits)
                {
                    //Determine if entity is in direction of movement.
                    Vector2 expectedNormal = rightward ? Vector2.left : Vector2.right;
                    if(Vector2.Angle(expectedNormal, hit.normal) < 45.0f)
                    {
                        //Push entity out of the way.
                        float pushDistance = xSmallMove - hit.distance;
                        MovingEntity entity = hit.collider.gameObject.GetComponent<MovingEntity>();
                        entity.MoveCollideX(pushDistance, rightward);
                        pushedEntities.Add(entity);
                    }
                }

                //Move solid.
                rBody.position += new Vector2(rightward ? xSmallMove : -xSmallMove, 0.0f);

                //Carry any riding entity, unless it was already pushed.
                foreach(var entity in ridingEntities)
                {
                    if(!pushedEntities.Contains(entity))
                    {
                        //Carry entity.
                        entity.MoveCollideX(xSmallMove, rightward);
                    }
                }
            }

            if(yAmount > 0.0f)
            {
                //Determine movement amount for this step.
                float ySmallMove = yAmount > MOVE_UNIT ? MOVE_UNIT : yAmount;
                yAmount -= ySmallMove;

                //Simulate movement, to check for entities that should be pushed.
                RaycastHit2D[] hits = Physics2D.BoxCastAll(rBody.position, overlapBoxSize, curRotation, 
                        upward ? Vector2.up : Vector2.down, ySmallMove, ENTITY_LAYER_MASK);

                //Push any entities that are in the way.
                List<MovingEntity> pushedEntities = new();
                foreach(RaycastHit2D hit in hits)
                {
                    //Determine if entity is in direction of movement.
                    Vector2 expectedNormal = upward ? Vector2.down : Vector2.up;
                    if(Vector2.Angle(expectedNormal, hit.normal) < 45.0f)
                    {
                        //Push entity out of the way.
                        float pushDistance = ySmallMove - hit.distance;
                        MovingEntity entity = hit.collider.gameObject.GetComponent<MovingEntity>();
                        entity.MoveCollideY(pushDistance, upward);
                        pushedEntities.Add(entity);
                    }
                }

                //Move solid.
                rBody.position += new Vector2(0.0f, upward ? ySmallMove : -ySmallMove);

                //Carry any riding entity, unless it was already pushed.
                foreach(var entity in ridingEntities)
                {
                    if(!pushedEntities.Contains(entity))
                    {
                        //Carry entity.
                        entity.MoveCollideY(ySmallMove, upward);
                    }
                }
            }
        }

        hitbox.enabled = true;
    }

    //Find all entities in the level currently riding this object.
    private List<MovingEntity> FindAllRidingEntities(Vector2 curVelocity)
    {
        List<MovingEntity> entities = LevelManager.Instance.GetAllEntities();
        
        //Filter list of all entities down to the ones riding this object.
        return entities.Where(entity => entity.IsRidingObject(this, curVelocity)).ToList();
    }
}
