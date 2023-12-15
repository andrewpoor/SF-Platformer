using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingSolidPhysics : MonoBehaviour
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
}
