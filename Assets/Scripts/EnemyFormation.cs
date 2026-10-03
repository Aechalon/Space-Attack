using UnityEngine;
using System;
using System.Collections.Generic;

[DefaultExecutionOrder(-100)]
public class EnemyFormation : MonoBehaviour
{
    [Serializable]
    public class FormationRow
    {
        [SerializeField] private bool[] occupiedColumns;

        public FormationRow()
        {
            occupiedColumns = new bool[8];
        }

        public FormationRow(bool[] columns)
        {
            occupiedColumns = columns;
        }

        public bool IsOccupied(int column)
        {
            return occupiedColumns != null && column >= 0 && column < occupiedColumns.Length && occupiedColumns[column];
        }

        public void Resize(int columnCount)
        {
            if (occupiedColumns == null)
                occupiedColumns = new bool[columnCount];
            else if (occupiedColumns.Length != columnCount)
                Array.Resize(ref occupiedColumns, columnCount);
        }
    }

    [Header("References")]
    [SerializeField] private PlayerMovement poolOwner;
    [SerializeField] private DifficultyManager difficultyManager;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField, Min(0f)] private float nextWaveDelay = 2f;

    [Header("Formation Grid")]
    [SerializeField, Min(1)] private int rows = 5;
    [SerializeField, Min(1)] private int columns = 8;
    [SerializeField, Min(0.1f)] private float columnSpacing = 0.8f;
    [SerializeField, Min(0.1f)] private float rowSpacing = 0.65f;
    [SerializeField] private FormationRow[] rowMasks =
    {
        new FormationRow(new[] { false, true, true, false, false, true, true, false }),
        new FormationRow(new[] { false, true, true, true, true, true, true, false }),
        new FormationRow(new[] { true, true, true, true, true, true, true, true }),
        new FormationRow(new[] { true, true, false, true, true, false, true, true }),
        new FormationRow(new[] { true, true, false, true, true, false, true, true })
    };

    [Header("Hive Movement")]
    [SerializeField, Min(0f)] private float edgeStepDown = 0.2f;
    [SerializeField] private float minimumY = -1.5f;
    [SerializeField, Min(0f)] private float edgePadding = 0.5f;

    [Header("Diving")]
    [SerializeField, Min(0f)] private float passBelowPlayerDistance = 0.8f;
    [SerializeField, Min(0f)] private float diveCurveWidth = 1.5f;

    [Header("Enemy Fire")]
    [SerializeField, Min(0.1f)] private float enemyShotInterval = 2.5f;
    [SerializeField, Min(0f)] private float enemyBulletSpeed = 3.5f;

    private readonly HashSet<Enemy> activeEnemies = new HashSet<Enemy>();
    private readonly HashSet<Enemy> divers = new HashSet<Enemy>();
    private readonly List<Enemy> diveCandidates = new List<Enemy>();
    private float waveSwaySpeed;
    private float diveSpeed;
    private float diveInterval;
    private float diveTimer;
    private float enemyShotTimer;
    private float waveTimer;
    private int maxDivers;
    private float horizontalDirection = 1f;
    private bool waveActive;
    private bool waitingForNextWave;
    private bool playerDied;

    private void Start()
    {
        if (poolOwner == null)
            poolOwner = FindFirstObjectByType<PlayerMovement>();
        if (difficultyManager == null)
            difficultyManager = GetComponent<DifficultyManager>();
        if (difficultyManager == null)
            difficultyManager = FindFirstObjectByType<DifficultyManager>();
        if (gameplayCamera == null && poolOwner != null)
            gameplayCamera = poolOwner.GameplayCamera;
        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        if (poolOwner == null)
            Debug.LogError("EnemyFormation requires a PlayerMovement reference to use the enemy pool.", this);
        if (difficultyManager == null)
            Debug.LogError("EnemyFormation requires a DifficultyManager reference.", this);
        if (gameplayCamera == null)
            Debug.LogError("EnemyFormation requires a gameplay camera.", this);

        if (poolOwner == null || difficultyManager == null || gameplayCamera == null)
            return;

        poolOwner.Died += HandlePlayerDied;
        ResetRootPosition();
        StartWave();
    }

    private void Update()
    {
        if (playerDied)
            return;

        if (waitingForNextWave)
        {
            waveTimer -= Time.deltaTime;
            if (waveTimer <= 0f)
            {
                waitingForNextWave = false;
                ResetRootPosition();
                StartWave();
            }
            return;
        }

        if (!waveActive)
            return;

        if (divers.Count < maxDivers)
        {
            diveTimer += Time.deltaTime;
            if (diveTimer >= diveInterval)
            {
                StartRandomDive();
                diveTimer = 0f;
            }
        }
        else
            diveTimer = 0f;

        enemyShotTimer += Time.deltaTime;
        if (enemyShotTimer >= enemyShotInterval)
        {
            FireEnemyShot();
            enemyShotTimer = 0f;
        }
    }

    private void FixedUpdate()
    {
        if (!waveActive || playerDied || gameplayCamera == null)
            return;

        Vector3 position = transform.position;
        float distance = waveSwaySpeed * Time.fixedDeltaTime;
        float cameraDepth = gameplayCamera.WorldToViewportPoint(position).z;
        float minX = gameplayCamera.ViewportToWorldPoint(new Vector3(0f, 0.5f, cameraDepth)).x + FormationHalfWidth() + edgePadding;
        float maxX = gameplayCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, cameraDepth)).x - FormationHalfWidth() - edgePadding;

        if (maxX <= minX)
        {
            position.x = (minX + maxX) * 0.5f;
        }
        else
        {
            position.x += horizontalDirection * distance;
            if (position.x >= maxX || position.x <= minX)
            {
                position.x = Mathf.Clamp(position.x, minX, maxX);
                horizontalDirection *= -1f;
                position.y = Mathf.Max(minimumY, position.y - edgeStepDown);
            }
        }

        transform.position = position;
    }

    private void StartWave()
    {
        waveSwaySpeed = difficultyManager.FormationSwaySpeed;
        diveSpeed = difficultyManager.DiveSpeed;
        diveInterval = difficultyManager.DiveInterval;
        maxDivers = difficultyManager.MaxDivers;
        diveTimer = 0f;
        enemyShotTimer = 0f;

        List<Vector3> slots = BuildSlots();
        if (slots.Count == 0)
        {
            Debug.LogError("EnemyFormation has no occupied grid slots. Enable at least one column in a row mask.", this);
            return;
        }

        foreach (Vector3 slot in slots)
        {
            Enemy enemy = poolOwner.GetPooledEnemy();
            if (enemy == null)
                break;

            enemy.transform.SetPositionAndRotation(transform.TransformPoint(slot), transform.rotation);
            enemy.Initialize(gameplayCamera, poolOwner, transform, slot, HandleEnemyReturned, HandleEnemyRejoined);
            activeEnemies.Add(enemy);
            enemy.gameObject.SetActive(true);
        }

        waveActive = activeEnemies.Count > 0;
        if (!waveActive)
            Debug.LogError("EnemyFormation could not get enemies from the PlayerMovement pool.", this);
    }

    private List<Vector3> BuildSlots()
    {
        List<Vector3> slots = new List<Vector3>();
        for (int row = 0; row < rows; row++)
        {
            if (rowMasks == null || row >= rowMasks.Length || rowMasks[row] == null)
                continue;

            for (int column = 0; column < columns; column++)
            {
                if (!rowMasks[row].IsOccupied(column))
                    continue;

                float x = (column - (columns - 1) * 0.5f) * columnSpacing;
                float y = -row * rowSpacing;
                slots.Add(new Vector3(x, y, 0f));
            }
        }

        return slots;
    }

    private void StartRandomDive()
    {
        diveCandidates.Clear();
        foreach (Enemy enemy in activeEnemies)
        {
            if (enemy.State == EnemyState.Formation)
                diveCandidates.Add(enemy);
        }

        if (diveCandidates.Count == 0)
            return;

        Enemy diver = diveCandidates[UnityEngine.Random.Range(0, diveCandidates.Count)];
        divers.Add(diver);
        diver.BeginDive(diveSpeed, passBelowPlayerDistance, diveCurveWidth);
    }

    private void FireEnemyShot()
    {
        diveCandidates.Clear();
        foreach (Enemy enemy in activeEnemies)
        {
            if (enemy.State == EnemyState.Formation)
                diveCandidates.Add(enemy);
        }

        if (diveCandidates.Count == 0)
            return;

        Enemy shooter = diveCandidates[UnityEngine.Random.Range(0, diveCandidates.Count)];
        Vector2 direction = poolOwner.PlayerTransform.position - shooter.transform.position;
        poolOwner.FireEnemyBullet(shooter.transform.position, direction, enemyBulletSpeed);
    }

    private void HandleEnemyReturned(Enemy enemy, EnemyReturnReason reason)
    {
        if (!activeEnemies.Remove(enemy))
            return;

        divers.Remove(enemy);
        poolOwner.ReturnEnemyToPool(enemy);

        if (reason == EnemyReturnReason.Killed)
            difficultyManager.RegisterKill();
        else if (reason == EnemyReturnReason.MissedBottom)
            poolOwner.TakeDamage(1);

        if (activeEnemies.Count == 0 && !playerDied)
            ClearWave();
    }

    private void HandleEnemyRejoined(Enemy enemy)
    {
        divers.Remove(enemy);
    }

    private void ClearWave()
    {
        waveActive = false;
        difficultyManager.RegisterWaveCleared();
        waitingForNextWave = true;
        waveTimer = nextWaveDelay;
    }

    private void ResetRootPosition()
    {
        float distance = gameplayCamera.WorldToViewportPoint(transform.position).z;
        Vector3 position = gameplayCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.82f, distance));
        position.z = transform.position.z;
        transform.position = position;
        horizontalDirection = 1f;
    }

    private float FormationHalfWidth()
    {
        return (columns - 1) * columnSpacing * 0.5f;
    }

    private void HandlePlayerDied()
    {
        playerDied = true;
        waveActive = false;
        waitingForNextWave = false;
    }

    private void OnValidate()
    {
        rows = Mathf.Max(1, rows);
        columns = Mathf.Max(1, columns);
        columnSpacing = Mathf.Max(0.1f, columnSpacing);
        rowSpacing = Mathf.Max(0.1f, rowSpacing);
        edgeStepDown = Mathf.Max(0f, edgeStepDown);
        edgePadding = Mathf.Max(0f, edgePadding);
        passBelowPlayerDistance = Mathf.Max(0.1f, passBelowPlayerDistance);
        diveCurveWidth = Mathf.Max(0f, diveCurveWidth);
        enemyShotInterval = Mathf.Max(0.1f, enemyShotInterval);
        enemyBulletSpeed = Mathf.Max(0f, enemyBulletSpeed);
        if (rowMasks == null)
            rowMasks = new FormationRow[rows];
        else if (rowMasks.Length != rows)
            Array.Resize(ref rowMasks, rows);

        for (int row = 0; row < rowMasks.Length; row++)
        {
            if (rowMasks[row] == null)
                rowMasks[row] = new FormationRow(new bool[columns]);
            rowMasks[row].Resize(columns);
        }
    }

    private void OnDestroy()
    {
        if (poolOwner != null)
            poolOwner.Died -= HandlePlayerDied;
    }
}
