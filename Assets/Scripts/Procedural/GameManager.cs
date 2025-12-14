using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Security.AccessControl;

// GameManager orchestrates the core game loop for the procedurally generated endless pod racing game.
public class GameManager : MonoBehaviour
{
    [Header("Game Screen")]
    public GameObject fighter;
    public GameObject gameUI;
    public Stopwatch stopwatch;
    public TextMeshProUGUI stopwatchText;
    
    [Header("Game Over Screen")]
    public GameObject gameOverUI;
    public TMP_Text scoreText;
    public Button restartButton; 
    public Button quitButton; 

    bool crashed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    // Includes when game is restarted.
    void Start()
    {  
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        crashed = false;
        gameOverUI.SetActive(false);
   
        restartButton.onClick.AddListener(Restart);
        quitButton.onClick.AddListener(Quit);

        stopwatch = new();
        stopwatch.Start();
        stopwatchText.SetText("00:00.00");
        gameUI.SetActive(true);
        fighter.SetActive(true);
    }

    // Updates the on-screen timer display in real-time during gameplay.

    void FixedUpdate()
    {
        float elapsedSeconds = (float)stopwatch.Elapsed.TotalSeconds % 60;
        int elapsedMinutes = (int)stopwatch.Elapsed.TotalMinutes;
        stopwatchText.SetText(string.Format("{0:00}:{1:00.00}", elapsedMinutes, elapsedSeconds));
    }


    // Detects collision with terrain sections. Uses a flag to prevent multiple GameOver triggers.

    void OnCollisionEnter(Collision other)
    {
        if (!crashed) {
            crashed = true;
            GameOver();
        }
    }


    // Handles game-over state: pauses gameplay, displays score/performance data, and shows UI for restart options.
    public void GameOver()
    {
        stopwatch.Stop();
        gameUI.SetActive(false);
        fighter.SetActive(false);

        // Display final score combining survival time and peak speed achieved during the run
        scoreText.text = "Time: " + stopwatchText.text + "  Top Speed: " + MoveSection.metersPerSecond + " m/s";
        SectionDestroyer.Stop();
        gameOverUI.SetActive(true);
        UnityEngine.Debug.Log("Game Over x_x");

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

    }

    public void Restart(){
        UnityEngine.Debug.Log("Restart: ^_^");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Quit(){
        UnityEngine.Debug.Log("Quit: ByeBye");
        Application.Quit();
    }
}