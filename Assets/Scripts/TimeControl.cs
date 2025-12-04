using System.Diagnostics;
using TMPro;
using UnityEngine;

public class TimeControl : MonoBehaviour
{
    public TextMeshProUGUI stopwatchText;

    Stopwatch stopwatch;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        stopwatch = new();
        stopwatch.Start();
    }

    void FixedUpdate()
    {
        float elapsedSeconds = (float)stopwatch.Elapsed.TotalSeconds % 60;
        int elapsedMinutes = (int)stopwatch.Elapsed.TotalMinutes;
        stopwatchText.SetText(string.Format("{0:00}:{1:00.00}", elapsedMinutes, elapsedSeconds));
    }

    void OnTriggerEnter(Collider other)
    {
        if (stopwatch.IsRunning) {
            stopwatch.Stop();
        }
    }

    void Reset()
    {
        stopwatch.Reset();
        stopwatchText.SetText("00:00.00");
    }
}
