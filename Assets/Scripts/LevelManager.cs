using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    private List<MovingEntity> entities = new();

    public List<MovingEntity> GetAllEntities()
    {
        return entities;
    }

    public void RegisterEntity(MovingEntity entity)
    {
        entities.Add(entity);
    }

    public void UnregisterEntity(MovingEntity entity)
    {
        entities.Remove(entity);
    }

    void Awake()
    {
        if(Instance != null)
        {
            Debug.LogError("Multiple instances of level manager. There should only be one.");
            return;
        }

        Instance = this;
    }
}
