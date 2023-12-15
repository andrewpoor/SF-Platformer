using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    private List<GameObject> entities = new();

    public List<GameObject> GetAllEntities()
    {
        return entities;
    }

    public void RegisterEntity(GameObject entity)
    {
        entities.Add(entity);
    }

    public void UnregisterEntity(GameObject entity)
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
