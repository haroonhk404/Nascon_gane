using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// Scene mein ek empty object pe lagao. Jab required candles lit ho jayein to door unlock.
/// Pet isi se poochta hai ke player ko kahan guide karna hai.
/// </summary>
public class CandlePuzzleManager : MonoBehaviour
{
    public static CandlePuzzleManager Instance { get; private set; }

    [Header("Candles")]
    [Tooltip("Shuru wali jalti hui candle (count nahi hoti)")]
    public Candle starterCandle;
    [Tooltip("Woh candles jo player ne jalani hain")]
    public List<Candle> puzzleCandles = new List<Candle>();
    public int requiredLitCount = 3;

    [Header("References")]
    public LockedDoor door;
    public PlayerInteractor player;
    [Tooltip("Optional: 'Candles 1/3' wala text")]
    public TMP_Text progressText;

    [Header("Events")]
    public UnityEvent onPuzzleSolved;

    public bool Solved { get; private set; }

    public int LitCount
    {
        get
        {
            int n = 0;
            foreach (var c in puzzleCandles) if (c && c.IsLit) n++;
            return n;
        }
    }

    void Awake() => Instance = this;

    void Start()
    {
        requiredLitCount = Mathf.Clamp(requiredLitCount, 1, Mathf.Max(1, puzzleCandles.Count));
        foreach (var c in puzzleCandles)
            if (c) c.OnLit += HandleCandleLit;

        if (door) door.isLocked = true;
        UpdateUI();
    }

    void HandleCandleLit(Candle c)
    {
        UpdateUI();
        if (!Solved && LitCount >= requiredLitCount)
        {
            Solved = true;
            if (door) door.Unlock();
            if (player) player.ShowMessage("Door Unlocked!");
            onPuzzleSolved?.Invoke();
        }
    }

    void UpdateUI()
    {
        if (progressText)
            progressText.text = Solved ? "Door Unlocked!" : $"Candles: {LitCount}/{requiredLitCount}";
    }

    /// <summary>Pet ke liye: player ko abhi kahan jana chahiye?</summary>
    public Transform GetGuideTarget()
    {
        if (Solved)
            return (door && !door.Used) ? door.GuidePoint : null;   // door use ho gaya -> pet waypoints follow kare

        // Pehle starter candle grab karwao
        if (starterCandle && !starterCandle.IsHeld && (player == null || player.HeldCandle == null))
            return starterCandle.transform;

        // Phir sab se qareeb wali bujhi hui candle
        Vector3 from = player ? player.transform.position : transform.position;
        Candle best = null;
        float bestDist = float.MaxValue;
        foreach (var c in puzzleCandles)
        {
            if (!c || c.IsLit) continue;
            float d = (c.transform.position - from).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = c; }
        }
        return best ? best.transform : null;
    }
}
