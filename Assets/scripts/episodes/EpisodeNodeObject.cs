using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EpisodeNodeObject : MonoBehaviour
{
    public delegate void ReadyToStartLoop();

    public EpisodeNode Node;
    protected GameManager gameManager_;

    private List<SpawnedObject> spawnedPrefabs_ = new List<SpawnedObject>();
    private float timer_ = 0f;

    private RectTransform spawnedObjectParent_;

    private void Start()
    {
        GameObject o = new GameObject("spawned prefab parent");
        o.AddComponent<RectTransform>();
        o.transform.SetParent(transform);
        spawnedObjectParent_ = o.GetComponent<RectTransform>();
        spawnedObjectParent_.transform.localScale = Vector3.one;
        spawnedObjectParent_.transform.localPosition = Vector3.zero;
    }

    public virtual void Init(GameManager manager, EpisodeNode node)
    {
        gameManager_ = manager;
        Node = node;
    }

    public virtual bool IsPlaying
    {
        get
        {
            return false;
        }
    }

    public virtual void Play()
    {
        ResetSpawnedObjects();
        ResetCommandLines();
    }

    public virtual void ReceiveAction(string action)
    {
        foreach(SpawnedObject o in spawnedPrefabs_)
        {
            o.ReceivedAction(action);
        }
    }

    public virtual float ProgressPercentage
    {
        get
        {
            return -1f;
        }
    }

    private void Update()
    {
        spawnedObjectParent_.SetAsLastSibling();
           
        if (IsPlaying)
        {
            timer_ += Time.deltaTime;
        }

        foreach(EpisodeNode.PrefabSpawnObject o in Node.PrefabSpawnObjects)
        {
            if (timer_ > o.TimeStamp)
            {
                if (!o.Spawned)
                {
                    SpawnObject(o);
                }
            }
        }

        foreach (EpisodeNode.CommandLine c in Node.CommandLines)
        {
            if (timer_ > c.TimeStamp)
            {
                if (!c.Ran)
                {
                    gameManager_.NewNodeAction(GameManager.ACTION_PREFIX + c.Command);
                    c.Ran = true;
                }
            }
        }
    }

    private void SpawnObject(EpisodeNode.PrefabSpawnObject prefabSpawnObject)
    {
        SpawnedObject o = Resources.Load<SpawnedObject>(prefabSpawnObject.Path);
        SpawnedObject spawnedObject = GameObject.Instantiate<SpawnedObject>(o, spawnedObjectParent_);
        spawnedObject.transform.localPosition = prefabSpawnObject.Position;
        spawnedObject.transform.localScale = prefabSpawnObject.Scale;
        spawnedObject.Init(gameManager_);

        spawnedPrefabs_.Add(spawnedObject);

        prefabSpawnObject.Spawned = true;
    }

    private void ResetSpawnedObjects()
    {
        timer_ = 0f;
        foreach (EpisodeNode.PrefabSpawnObject o in Node.PrefabSpawnObjects)
        {
            o.Spawned = false;
        }
        for (int i = 0; i < spawnedPrefabs_.Count; i++)
        {
            SpawnedObject o = spawnedPrefabs_[i];
            Destroy(o.gameObject);
        }
        spawnedPrefabs_ = new List<SpawnedObject>();
    }

    private void ResetCommandLines()
    {
        timer_ = 0f;
        foreach(EpisodeNode.CommandLine c in Node.CommandLines)
        {
            c.Ran = false;
        }
    }
}
