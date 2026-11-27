using System.Collections.Generic;
using UnityEngine;

public enum EntityRole
{
    Platform,
    Player,
    InstakillSpike,
    Enemy,
    PowerupFireball,
    PowerupLife,
    PowerupInvincible,
    FireballProjectile,
    Goal
}

public class AABBBounds
{
    public Vector3 Center { get; private set; }
    public Vector3 Size { get; private set; }
    public Vector3 Extents { get; private set; }
    public Vector3 Min { get; private set; }
    public Vector3 Max { get; private set; }
    public int ID { get; private set; }
    public EntityRole Role { get; set; }

    public AABBBounds(Vector3 center, Vector3 size, int id, EntityRole role)
    {
        ID = id;
        Role = role;
        UpdateBounds(center, size);
    }

    public void UpdateBounds(Vector3 center, Vector3 size)
    {
        Center = center;
        Size = size;
        Extents = size * 0.5f;
        Min = center - Extents;
        Max = center + Extents;
    }

    public bool Intersects(AABBBounds other)
    {
        return !(Max.x < other.Min.x || Min.x > other.Max.x ||
                 Max.y < other.Min.y || Min.y > other.Max.y ||
                 Max.z < other.Min.z || Min.z > other.Max.z);
    }
}

public class CollisionManager : MonoBehaviour
{
    private static CollisionManager _instance;

    public static CollisionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("CollisionManager");
                _instance = go.AddComponent<CollisionManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private readonly Dictionary<int, AABBBounds> _colliders = new Dictionary<int, AABBBounds>();
    private int nextID = 0;

    public void ClearAll()
    {
        _colliders.Clear();
        nextID = 0;
    }

    public int RegisterCollider(Vector3 center, Vector3 size, EntityRole role)
    {
        int id = nextID++;
        _colliders[id] = new AABBBounds(center, size, id, role);
        return id;
    }

    public void UpdateCollider(int id, Vector3 center, Vector3 size)
    {
        if (_colliders.TryGetValue(id, out AABBBounds bounds))
            bounds.UpdateBounds(center, size);
    }

    public void RemoveCollider(int id)
    {
        _colliders.Remove(id);
    }

    public EntityRole GetRole(int id)
    {
        return _colliders.TryGetValue(id, out AABBBounds bounds) ? bounds.Role : EntityRole.Platform;
    }

    public bool CheckCollision(int id, Vector3 newCenter, out List<int> collidingIds)
    {
        collidingIds = new List<int>();
        if (!_colliders.TryGetValue(id, out AABBBounds current))
            return false;

        AABBBounds temp = new (newCenter, current.Size, -1, current.Role);

        foreach (var kvp in _colliders)
        {
            if (kvp.Key == id) continue;
            if (temp.Intersects(kvp.Value))
                collidingIds.Add(kvp.Key);
        }
        return collidingIds.Count > 0;
    }
}