using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MovingSolid : MonoBehaviour
{
    [SerializeField] private BoxCollider2D hitbox;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    //Move the solid the given amount. This will push or carry other entities,
    // but won't interact with other solids.
    public void Move(float x, float y)
    {

    }

    private List<MovingEntity> FindAllRidingEntities()
    {
        List<MovingEntity> entities = LevelManager.Instance.GetAllEntities();
        
        //Filter list of all entities down to the ones riding this object.
        return entities.Where(entity => entity.IsRidingObject(gameObject)).ToList();
    }
}
