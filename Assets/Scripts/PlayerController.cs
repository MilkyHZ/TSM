using System.Collections.Generic;
using UnityEngine;

public class PlayerController
{
    public int HP { get; set; } = 3;
    public bool HasFireball { get; set; }
    public float InvincibleTimer { get; set; }
    public bool IsInvincible => InvincibleTimer > 0f;
    public int FacingDir { get; private set; } = 1;

    private readonly GameWorld world;
    private readonly PlayerCameraFollow cameraFollow;
    private readonly float speed;
    private readonly float jumpForce;
    private readonly float riseGravity;
    private readonly float fallGravity;

    private Vector3 velocity;
    private bool isGrounded;
    private float coyoteTimer;
    private float jumpBufferTimer;

    public PlayerController(GameWorld world, PlayerCameraFollow cam, float speed, float jumpForce, float riseGrav, float fallGrav)
    {
        this.world = world;
        cameraFollow = cam;
        this.speed = speed;
        this.jumpForce = jumpForce;
        riseGravity = riseGrav;
        fallGravity = fallGrav;
    }

    public HashSet<int> Tick(float dt)
    {
        if (InvincibleTimer > 0f) InvincibleTimer -= dt;

        if (Input.GetKeyDown(KeyCode.Space))
            jumpBufferTimer = 0.12f;
        else
            jumpBufferTimer -= dt;

        if (isGrounded)
            coyoteTimer = 0.10f;
        else
            coyoteTimer -= dt;

        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            velocity.y = jumpForce;
            isGrounded = false;
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
        }

        if (isGrounded && velocity.y <= 0f)
            velocity.y = -2f;
        else
            velocity.y -= (velocity.y > 0f ? riseGravity : fallGravity) * dt;

        float input = 0f;
        if (Input.GetKey(KeyCode.A)) { input -= 1f; FacingDir = -1; }
        if (Input.GetKey(KeyCode.D)) { input += 1f; FacingDir = 1; }
        float activeSpeed = isGrounded ? speed : speed * 0.5f;

        Vector3 pos = world.PlayerItem.position;
        var hits = new HashSet<int>();

        Vector3 targetX = pos + new Vector3(input * activeSpeed * dt, 0f, 0f);
        if (!HitsSolid(targetX, hits)) pos = targetX;

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

        world.UpdateItemTransform(world.PlayerItem, pos);
        cameraFollow?.SetPlayerPosition(pos);
        return hits;
    }

    private bool HitsSolid(Vector3 proposedPos, HashSet<int> hits)
    {
        bool blocked = false;
        if (CollisionManager.Instance.CheckCollision(world.PlayerItem.colliderId, proposedPos, out var ids))
        {
            foreach (int id in ids)
            {
                hits.Add(id);
                if (CollisionManager.Instance.GetRole(id) == EntityRole.Platform)
                    blocked = true;
            }
        }
        return blocked;
    }

    public void Respawn(Vector3 spawnPos)
    {
        velocity = Vector3.zero;
        isGrounded = false;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        world.UpdateItemTransform(world.PlayerItem, spawnPos);
    }
}