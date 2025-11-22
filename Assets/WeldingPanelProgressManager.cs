using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class WeldingPanelProgressManager : MonoBehaviour
{
    public WeldingPanel[] panels;
    public float completeThreshold = 0.80f; // 80%
    public float noWeldDelay = 5f;         // 5 seconds without welding

    private int currentIndex = 0;
    private float noWeldTimer = 0f;
    private bool isWelding = false;

    private WeldingHandle handle;
    private UIcontrols ui;

    [Header("Completion Text")]
    public TMP_Text  completionText; // Good / Excellent

    void Start()
    {
        handle = FindObjectOfType<WeldingHandle>();
        ui = FindObjectOfType<UIcontrols>();

        if (completionText != null)
            completionText.gameObject.SetActive(false);

        // Initialize all panels
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null)
            {
                panels[i].InitializePanel();
                panels[i].gameObject.SetActive(i == 0); // Only first panel active
            }
        }
    }

    void Update()
    {
        if (currentIndex >= panels.Length) return;

        WeldingPanel currentPanel = panels[currentIndex];
        if (currentPanel == null) return;

        isWelding = handle != null && handle.IsWelding();

        if (isWelding)
            noWeldTimer = 0f;
        else
            noWeldTimer += Time.deltaTime;

        if (noWeldTimer >= noWeldDelay)
        {
            float progress = currentPanel.GetCurrentCoverage();

            if (progress >= completeThreshold)
            {
                CompleteCurrentPanel(currentPanel, progress);
            }
        }
    }

    private void CompleteCurrentPanel(WeldingPanel panel, float progress)
    {
        // Complete panel visuals
        panel.CompletePanel();

        // Activate panel inventory image
        if (panel.inventoryImage != null)
            panel.inventoryImage.SetActive(true);

        // Hide the panel in workspace
        panel.gameObject.SetActive(false);

        // Show Good / Excellent text
        ShowCompletionText(progress);

        // Move to next panel
        HandlePanelSwitch();
    }

    private void ShowCompletionText(float progress)
    {
        if (completionText == null) return;

        completionText.gameObject.SetActive(true);
        completionText.text = (progress >= 1f) ? "Excellent!" : "Good!";

        CancelInvoke(nameof(HideCompletionText));
        Invoke(nameof(HideCompletionText), 2f); // Hide after 2 seconds
    }

    private void HideCompletionText()
    {
        if (completionText != null)
            completionText.gameObject.SetActive(false);
    }

    private void HandlePanelSwitch()
    {
        currentIndex++;

        if (currentIndex >= panels.Length)
        {
            Debug.Log("All welding panels completed!");
            if (ui != null) ui.BackButton();
            Invoke(nameof(ReloadScene), 5f);
            return;
        }

        if (ui != null)
        {
            ui.Moveback(); // Play MoveBack animation
            Invoke(nameof(DelayedMoveToWeld), 5f); // Then MoveToWeld after 5 sec
        }

        // Activate next panel
        if (panels[currentIndex] != null)
            panels[currentIndex].gameObject.SetActive(true);
    }

    private void DelayedMoveToWeld()
    {
        if (ui != null)
            ui.MoveToWeld();
    }

    private void ReloadScene()
    {
        if (ui != null)
            ui.Reload();
    }
}
