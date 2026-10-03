using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

public class DifficultyManager : MonoBehaviour
{
    [Header("Dive Interval")]
    [SerializeField, Min(0.1f)] private float startingDiveInterval = 4f;
    [SerializeField, Min(0.1f)] private float minimumDiveInterval = 0.7f;
    [SerializeField, Min(0f)] private float diveIntervalDecreasePerLevel = 0.2f;

    [Header("Dive Speed")]
    [SerializeField, Min(0f)] private float startingDiveSpeed = 4f;
    [SerializeField, Min(0f)] private float diveSpeedIncreasePerLevel = 0.5f;
    [SerializeField, Min(0f)] private float maximumDiveSpeed = 12f;

    [Header("Maximum Divers")]
    [SerializeField, Min(1)] private int startingMaxDivers = 1;
    [SerializeField, Min(0)] private int maxDiversIncreasePerLevel = 1;
    [SerializeField, Min(1)] private int maximumDivers = 4;

    [Header("Formation Sway")]
    [SerializeField, Min(0f)] private float startingFormationSwaySpeed = 0.7f;
    [SerializeField, Min(0f)] private float formationSwayIncreasePerLevel = 0.15f;
    [SerializeField, Min(0f)] private float maximumFormationSwaySpeed = 3f;

    [Header("Score UI")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI enemyKilledText;
    [SerializeField] private TextMeshProUGUI difficultyText;
    [SerializeField, Min(0)] private int scorePerKill = 100;

    public event Action<int> EnemyKilled;
    public event Action<int> LevelUp;

    public int KillCount { get; private set; }
    public int Score { get; private set; }
    public int CurrentLevel { get; private set; } = 1;

    public float DiveInterval => Mathf.Max(
        minimumDiveInterval,
        startingDiveInterval - (CurrentLevel - 1) * diveIntervalDecreasePerLevel);

    public float DiveSpeed => Mathf.Min(
        maximumDiveSpeed,
        startingDiveSpeed + (CurrentLevel - 1) * diveSpeedIncreasePerLevel);

    public int MaxDivers => Mathf.Min(
        maximumDivers,
        startingMaxDivers + (CurrentLevel - 1) * maxDiversIncreasePerLevel);

    public float FormationSwaySpeed => Mathf.Min(
        maximumFormationSwaySpeed,
        startingFormationSwaySpeed + (CurrentLevel - 1) * formationSwayIncreasePerLevel);

    private void Awake()
    {
        RefreshText();
    }

    public void RegisterKill()
    {
        KillCount++;
        Score += scorePerKill;
        RefreshText();
        EnemyKilled?.Invoke(KillCount);
    }

    public void RegisterWaveCleared()
    {
        CurrentLevel++;
        RefreshText();
        LevelUp?.Invoke(CurrentLevel);
    }

    public void ResetDifficulty()
    {
        CurrentLevel = 1;
        RefreshText();
    }

    private void RefreshText()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {Score}";
        if (enemyKilledText != null)
            enemyKilledText.text = $"Enemies Killed: {KillCount}";
        if (difficultyText != null)
            difficultyText.text = $"Difficulty: {CurrentLevel}";
    }

    private void OnValidate()
    {
        startingDiveInterval = Mathf.Max(0.1f, startingDiveInterval);
        minimumDiveInterval = Mathf.Min(minimumDiveInterval, startingDiveInterval);
        startingDiveSpeed = Mathf.Max(0.1f, startingDiveSpeed);
        maximumDiveSpeed = Mathf.Max(maximumDiveSpeed, startingDiveSpeed);
        startingMaxDivers = Mathf.Max(1, startingMaxDivers);
        maximumDivers = Mathf.Max(maximumDivers, startingMaxDivers);
        startingFormationSwaySpeed = Mathf.Max(0f, startingFormationSwaySpeed);
        maximumFormationSwaySpeed = Mathf.Max(maximumFormationSwaySpeed, startingFormationSwaySpeed);
    }
}
