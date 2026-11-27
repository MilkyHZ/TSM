using System.Collections.Generic;
using TMPro;
using UnityEngine;

[System.Serializable]
public class LevelEntry
{
    public string label = "Block";
    public EntityRole role = EntityRole.Platform;
    public ShapeType shape = ShapeType.Cube;
    public Vector3 position;
    public Vector3 scale = Vector3.one;
}

[ExecuteAlways]
public class GameWorld : MonoBehaviour
{
    public static GameWorld Instance { get; private set; }

    [Header("Materials (Check 'Enable GPU Instancing')")]
    public Material playerMat;
    public Material platformMat;
    public Material hazardMat;
    public Material invincibilityMat;
    public Material fireMat;
    public Material firePowerMat;
    public Material healthMat;
    public Material goalMat;

    [Header("Player & Gameplay Settings")]
    public Vector3 playerStart = new (0, 2, 0);
    public float moveSpeed = 6f;
    public float jumpForce = 14f;
    public float riseGravity = 35f;
    public float fallGravity = 16f;
    public float enemySpeed = 2.5f;
    public float fireballSpeed = 14f;

    [Header("Dot Product Culling")]
    [Range(0f, 1f)] public float cullDotThreshold = 0.75f;

    [Header("UI")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI statusText;

    [Header("Level Layout")]
    public List<LevelEntry> levelLayout = new();

    public class WorldItem
    {
        public int colliderId;
        public ShapeType shape;
        public EntityRole role;
        public Vector3 position;
        public Vector3 scale;
        public Matrix4x4 matrix;
        public int moveDir = 1;
        public bool active = true;
    }

    public List<WorldItem> Items { get; } = new();
    public WorldItem PlayerItem { get; private set; }
    
    private List<EnemyBehaviour> enemies = new();
    private Mesh cubeMesh, pyramidMesh, sphereMesh;
    private PlayerController playerController;
    private float elapsedTime;
    private bool ended;

    private void OnEnable()
    {
        Instance = this;
        cubeMesh = ShapeMeshBuilder.BuildCube();
        pyramidMesh = ShapeMeshBuilder.BuildPyramid();
        sphereMesh = ShapeMeshBuilder.BuildSphere();

        if (Application.isPlaying)
            InitWorld();
    }

    [ContextMenu("Apply Level Layout Now")]
    public void ApplyLayoutNow()
    {
        if (Application.isPlaying)
            InitWorld();
    }

    public void InitWorld()
    {
        CollisionManager.Instance.ClearAll();
        Items.Clear();
        elapsedTime = 0f;
        ended = false;
        if (statusText) statusText.text = "";

        PlayerItem = AddItem(playerStart, Vector3.one, ShapeType.Cube, EntityRole.Player);

        foreach (var entry in levelLayout)
        {
            AddItem(entry.position, entry.scale, entry.shape, entry.role);
        }

        Camera cam = Camera.main;
        PlayerCameraFollow follow = cam != null ? cam.GetComponent<PlayerCameraFollow>() : null;
        if (cam != null && follow == null) follow = cam.gameObject.AddComponent<PlayerCameraFollow>();
        if (follow != null) follow.offset = new Vector3(0, 1, -15);

        enemies.Clear();
        foreach (var item in Items)
        {
            if (item.role == EntityRole.Enemy)
                enemies.Add(new EnemyBehaviour(this, item, enemySpeed, fallGravity));
        }

        playerController = new PlayerController(this, follow, moveSpeed, jumpForce, riseGravity, fallGravity);
    }

    public WorldItem AddItem(Vector3 pos, Vector3 scale, ShapeType shape, EntityRole role)
    {
        int id = CollisionManager.Instance.RegisterCollider(pos, scale, role);
        var item = new WorldItem
        {
            colliderId = id,
            shape = shape,
            role = role,
            position = pos,
            scale = scale,
            matrix = Matrix4x4.TRS(pos, Quaternion.identity, scale)
        };
        Items.Add(item);
        return item;
    }

    public void UpdateItemTransform(WorldItem item, Vector3 newPos)
    {
        item.position = newPos;
        item.matrix = Matrix4x4.TRS(newPos, Quaternion.identity, item.scale);
        CollisionManager.Instance.UpdateCollider(item.colliderId, newPos, item.scale);
    }

    public void RemoveItem(WorldItem item)
    {
        item.active = false;
        CollisionManager.Instance.RemoveCollider(item.colliderId);
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            DrawEditorPreview();
            return;
        }

        if (ended)
        {
            if (Input.GetKeyDown(KeyCode.R)) InitWorld();
            RenderWorld();
            return;
        }

        float dt = Time.deltaTime;
        elapsedTime += dt;

        var hits = playerController.Tick(dt);
        HandlePlayerHits(hits);

        if (PlayerItem.position.y < -15f)
        {
            playerController.HP = 0;
            EndGame("GAME OVER! Press R");
        }

        if (playerController.HasFireball && Input.GetKeyDown(KeyCode.F))
        {
            Vector3 spawn = PlayerItem.position + Vector3.right * ( playerController.FacingDir * 0.9f);
            var fb = AddItem(spawn, Vector3.one * 0.4f, ShapeType.Sphere, EntityRole.FireballProjectile);
            fb.moveDir = playerController.FacingDir;
        }

        UpdateEnemiesAndProjectiles(dt);
        UpdateUI();
        RenderWorld();
    }

    private void HandlePlayerHits(HashSet<int> hitIds)
    {
        foreach (int id in hitIds)
        {
            var item = Items.Find(x => x.active && x.colliderId == id);
            if (item == null) continue;

            switch (item.role)
            {
                case EntityRole.PowerupFireball:
                    playerController.HasFireball = true;
                    RemoveItem(item);
                    break;
                case EntityRole.PowerupLife:
                    playerController.HP++;
                    RemoveItem(item);
                    break;
                case EntityRole.PowerupInvincible:
                    playerController.InvincibleTimer = 8f;
                    RemoveItem(item);
                    break;
                case EntityRole.Enemy:
                    if (playerController.IsInvincible) RemoveItem(item);
                    else if (--playerController.HP <= 0) EndGame("GAME OVER! Press R");
                    else playerController.Respawn(playerStart);
                    break;
                case EntityRole.InstakillSpike:
                    if (!playerController.IsInvincible) { playerController.HP = 0; EndGame("GAME OVER! Press R"); }
                    break;
                case EntityRole.Goal:
                    EndGame("YOU WIN! Press R");
                    break;
            }
        }
    }

    private void UpdateEnemiesAndProjectiles(float dt)
    {
        foreach (var enemy in enemies)
        {
            if (!enemy.Item.active) continue;

            var hits = enemy.Tick(dt);

            if (hits.Contains(PlayerItem.colliderId))
            {
                if (playerController.IsInvincible)
                {
                    enemy.TakeDamage(999);
                }
                else if (--playerController.HP <= 0)
                {
                    EndGame("GAME OVER! Press R");
                }
                else
                {
                    playerController.Respawn(playerStart);
                }
            }
        }

        for (int i = Items.Count - 1; i >= 0; i--)
        {
            var item = Items[i];
            if (!item.active || item.role != EntityRole.FireballProjectile) continue;

            Vector3 next = item.position + new Vector3(item.moveDir * fireballSpeed * dt, 0f, 0f);
            bool destroy = Mathf.Abs(next.x - PlayerItem.position.x) > 30f;

            if (CollisionManager.Instance.CheckCollision(item.colliderId, next, out var ids))
            {
                foreach (int hit in ids)
                {
                    var r = CollisionManager.Instance.GetRole(hit);
                    if (r == EntityRole.Enemy)
                    {
                        var hitEnemy = enemies.Find(e => e.Item.active && e.Item.colliderId == hit);
                        if (hitEnemy != null) hitEnemy.TakeDamage(1);
                        destroy = true;
                    }
                    else if (r == EntityRole.Platform || r == EntityRole.InstakillSpike)
                    {
                        destroy = true;
                    }
                }
            }

            if (destroy) RemoveItem(item);
            else UpdateItemTransform(item, next);
        }
    }
    private void EndGame(string msg) { ended = true; if (statusText) statusText.text = msg; }

    private void UpdateUI()
    {
        if (timerText) timerText.text = $"Time: {elapsedTime:F1}s";
        if (hpText) hpText.text = $"HP: {playerController.HP}" +
                                  (playerController.HasFireball ? " [F: Fireball]" : "") +
                                  (playerController.IsInvincible ? " [INVINCIBLE]" : "");
    }

    private void DrawEditorPreview()
    {
        foreach (var list in batchGroups.Values) list.Clear();

        if (playerMat && cubeMesh)
        {
            var pKey = (cubeMesh, playerMat);
            if (!batchGroups.TryGetValue(pKey, out var pList))
                batchGroups[pKey] = pList = new List<Matrix4x4>();
            pList.Add(Matrix4x4.TRS(playerStart, Quaternion.identity, Vector3.one));
        }

        foreach (var entry in levelLayout)
        {
            Mesh m = GetMesh(entry.shape);
            Material mat = GetMaterial(entry.role);
            if (!m || !mat) continue;

            var key = (m, mat);
            if (!batchGroups.TryGetValue(key, out var list))
                batchGroups[key] = list = new List<Matrix4x4>();
            list.Add(Matrix4x4.TRS(entry.position, Quaternion.identity, entry.scale));
        }

        FlushBatches();
    }

    private void FlushBatches()
    {
        foreach (var kvp in batchGroups)
        {
            var list = kvp.Value;
            if (list.Count == 0) continue;

            int count = Mathf.Min(list.Count, 1023);
            for (int i = 0; i < count; i++) batchBuffer[i] = list[i];

            Graphics.DrawMeshInstanced(
                kvp.Key.Item1, 0, kvp.Key.Item2, batchBuffer, count,
                null, UnityEngine.Rendering.ShadowCastingMode.Off, false
            );
        }
    }

    private readonly Dictionary<(Mesh, Material), List<Matrix4x4>> batchGroups = new();
    private readonly Matrix4x4[] batchBuffer = new Matrix4x4[1023];

    private void RenderWorld()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        foreach (var list in batchGroups.Values) list.Clear();

        foreach (var item in Items)
        {
            if (!item.active) continue;
            Mesh m = GetMesh(item.shape);
            Material mat = GetMaterial(item.role);
            if (!m || !mat) continue;

            Vector3 toObj = (item.position - cam.transform.position).normalized;
            float dot = Vector3.Dot(cam.transform.forward, toObj);
            Vector3 finalScale = (item.role != EntityRole.Player && dot < cullDotThreshold) ? Vector3.zero : item.scale;

            var key = (m, mat);
            if (!batchGroups.TryGetValue(key, out var list))
            {
                list = new List<Matrix4x4>();
                batchGroups[key] = list;
            }
            list.Add(Matrix4x4.TRS(item.position, Quaternion.identity, finalScale));
        }

        foreach (var kvp in batchGroups)
        {
            var list = kvp.Value;
            if (list.Count == 0) continue;

            int count = Mathf.Min(list.Count, 1023);
            for (int i = 0; i < count; i++) batchBuffer[i] = list[i];

            Graphics.DrawMeshInstanced(kvp.Key.Item1, 0, kvp.Key.Item2, batchBuffer, count, null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
        }
    }
    private Mesh GetMesh(ShapeType shape) => shape switch
    {
        ShapeType.Pyramid => pyramidMesh,
        ShapeType.Sphere => sphereMesh,
        _ => cubeMesh
    };

    private Material GetMaterial(EntityRole role) => role switch
    {
        EntityRole.Player => (playerController != null && playerController.IsInvincible && invincibilityMat) ? invincibilityMat : playerMat,
        EntityRole.InstakillSpike => hazardMat,
        EntityRole.Enemy => hazardMat,
        EntityRole.PowerupFireball => fireMat,
        EntityRole.PowerupLife => healthMat,
        EntityRole.PowerupInvincible => invincibilityMat,
        EntityRole.FireballProjectile => firePowerMat,
        EntityRole.Goal => goalMat,
        _ => platformMat
    };
}