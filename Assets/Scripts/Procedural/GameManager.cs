using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Security.AccessControl;

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

    void FixedUpdate()
    {
        float elapsedSeconds = (float)stopwatch.Elapsed.TotalSeconds % 60;
        int elapsedMinutes = (int)stopwatch.Elapsed.TotalMinutes;
        stopwatchText.SetText(string.Format("{0:00}:{1:00.00}", elapsedMinutes, elapsedSeconds));
    }

    void OnCollisionEnter(Collision other)
    {
        if (!crashed) {
            crashed = true;
            GameOver();
        }
    }


    public void GameOver()
    {
        stopwatch.Stop();
        gameUI.SetActive(false);
        fighter.SetActive(false);
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
