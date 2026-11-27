using System.Collections.Generic;
using UnityEngine;

public class EnemyBehaviour
{
    public GameWorld.WorldItem Item { get; }
    public int HP { get; set; } = 1;
    public int FacingDir { get; private set; } = 1;

    private readonly GameWorld world;
    private readonly float speed;
    private readonly float gravity;
    private readonly bool turnAtEdges;

    private Vector3 velocity;
    private bool isGrounded;

    public EnemyBehaviour(GameWorld world, GameWorld.WorldItem item, float speed, float gravity = 20f, bool turnAtEdges = true)
    {
        this.world = world;
        Item = item;
        this.speed = speed;
        this.gravity = gravity;
        this.turnAtEdges = turnAtEdges;
    }

    public HashSet<int> Tick(float dt)
    {
        var hits = new HashSet<int>();
        if (!Item.active || HP <= 0) return hits;

        if (isGrounded) velocity.y = 0f;
        else velocity.y -= gravity * dt;

        Vector3 pos = Item.position;

        if (turnAtEdges && isGrounded)
        {
            Vector3 edgeProbe = pos + new Vector3(FacingDir * (Item.scale.x * 0.5f + 0.1f), -0.2f, 0f);
            if (!HasGroundBelow(edgeProbe))
            {
                FacingDir *= -1;
            }
        }

        Vector3 targetX = pos + new Vector3(FacingDir * speed * dt, 0f, 0f);
        if (!HitsSolid(targetX, hits))
        {
            pos = targetX;
        }
        else
        {
            FacingDir *= -1;
        }

        Vector3 targetY = pos + new Vector3(0f, velocity.y * dt, 0f);
        if (!HitsSolid(targetY, hits))
        {
            pos = targetY;
            isGrounded = false;
        }
        else
        {
            if (velocity.y < 0f) isGrounded = true;
            velocity.y = 0f;
        }

        world.UpdateItemTransform(Item, pos);
        return hits;
    }

    private bool HitsSolid(Vector3 proposedPos, HashSet<int> hits)
    {
        bool blocked = false;
        if (CollisionManager.Instance.CheckCollision(Item.colliderId, proposedPos, out var ids))
        {
            foreach (int id in ids)
            {
                hits.Add(id);
                var role = CollisionManager.Instance.GetRole(id);
                if (role == EntityRole.Platform || role == EntityRole.InstakillSpike || role == EntityRole.Enemy)
                    blocked = true;
            }
        }
        return blocked;
    }

    private bool HasGroundBelow(Vector3 probePos)
    {
        if (CollisionManager.Instance.CheckCollision(Item.colliderId, probePos, out var ids))
        {
            foreach (int id in ids)
            {
                if (CollisionManager.Instance.GetRole(id) == EntityRole.Platform)
                    return true;
            }
        }
        return false;
    }

    public void TakeDamage(int amount = 1)
    {
        HP -= amount;
        if (HP <= 0)
        {
            world.RemoveItem(Item);
        }
    }
}