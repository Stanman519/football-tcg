using System;
using UnityEngine;
using UnityEngine.UI; // For Button, Text, etc.

[System.Serializable]
public class PlayCallUIScript : MonoBehaviour
{
    // References to different panels
    [Header("Panels")]
    public GameObject playCallPanel;  // for offense/defense choices
/*    public GameObject coveragePanel;  // if you want a separate panel for coverage
    public GameObject slotResultPanel;*/

    // Example: references to buttons in the playCallPanel
    [Header("Offense Buttons")]
    [field: SerializeField]
    public Button runButton;
    public Button shortPassButton;
    public Button longPassButton;

    // Example: references to coverage buttons
/*    [Header("Defense Buttons")]
    public Button coverRunButton;
    public Button coverShortPassButton;
    public Button coverLongPassButton;*/

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Console.WriteLine("huh");
        // Set up the onClick events for the offense choice
        runButton.onClick.AddListener(() => OnOffensePlayChosen("Run"));
        shortPassButton.onClick.AddListener(() => OnOffensePlayChosen("ShortPass"));
        longPassButton.onClick.AddListener(() => OnOffensePlayChosen("LongPass"));

        // Similarly for defense
/*        coverRunButton.onClick.AddListener(() => OnDefenseCoverageChosen("Run"));
        coverShortPassButton.onClick.AddListener(() => OnDefenseCoverageChosen("ShortPass"));
        coverLongPassButton.onClick.AddListener(() => OnDefenseCoverageChosen("LongPass"));*/

        // Hide panels at start or show only the relevant ones
        playCallPanel.SetActive(true);
        /*coveragePanel.SetActive(false);
        slotResultPanel.SetActive(false);*/
    }

    // Called by GameManager when it's time to display the offense's choice
    public void ShowPlayCallUI()
    {
        playCallPanel.SetActive(true);
        //coveragePanel.SetActive(false);
    }

    // Called by the button click for offense
    private void OnOffensePlayChosen(string playType)
    {
        // Hide the panel once chosen
        playCallPanel.SetActive(false);
        Console.WriteLine("hellowrld.");
        // Now call the GameManager to tell it what the offense decided
        GameManager.Instance.OnOffensePlaySelected(playType);
    }
/*
    // Called by GameManager for defense coverage
    public void ShowCoverageUI()
    {
        coveragePanel.SetActive(true);
    }

    private void OnDefenseCoverageChosen(string coverageType)
    {
        coveragePanel.SetActive(false);
        //GameManager.Instance.OnDefenseCoverageSelected(coverageType);
    }*/

    // Example method to show slot results
    public void ShowSlotResult(string resultText)
    {
        //slotResultPanel.SetActive(true);
        // you might have a text field for showing the result
    }

    public void HideAll()
    {
        playCallPanel.SetActive(false);
/*        coveragePanel.SetActive(false);
        slotResultPanel.SetActive(false);*/
    }
}

