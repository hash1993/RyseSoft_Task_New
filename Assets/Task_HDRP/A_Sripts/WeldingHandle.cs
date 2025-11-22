using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeldingHandle : MonoBehaviour
{
    public Transform weldBlobSet, weldHoleMask, weldingTip;
    public MeshRenderer tipRenderer;
    public GameObject glowEffect;

    private AudioSource audioSource;
    private Material tipOriginalMat;

    private WeldingPanel panel;
    private WeldingPanel previousPanel;

    private RaycastHit weldHit;
    private bool isWeldingLayer = false;
    private float weldTimer;
    private float travelTimer;

    bool holdOn = false;

    private bool hasBlob = false;
    private Transform currentBlob;
    private float blobSizeTimer;
    private Transform currentPanel;
    Transform previousBlob;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        tipOriginalMat = tipRenderer.material;
    }
    public bool IsWelding()
{
    return isWeldingLayer;
}
    private void Update()
    {
        // Assign panel only when raycast hits valid panel
        if (weldHit.transform != null && weldHit.transform.parent != null)
            panel = weldHit.transform.parent.GetComponent<WeldingPanel>();
        else
            panel = null;

        // If we switched panels
        if (panel != previousPanel)
        {
            if (panel != null)
            {
                // Restore panel's previous saved coverage
                float coverage = panel.GetCurrentCoverage();

                if (panel.weldingProgressSlider != null)
                    panel.weldingProgressSlider.value = coverage;

                if (panel.weldingProgressText != null)
                    panel.weldingProgressText.text = Mathf.RoundToInt(coverage * 100f) + "%";
            }

            previousPanel = panel;
        }
    }

    public void StartWelding()
    {
        if (holdOn)
            return;

        weldTimer += Time.deltaTime;

        //Delay start
        if (weldTimer >= 1f)
        {
            if (isWeldingLayer)
            {
                ShowEffects(true);
                ShowBlob();
                travelTimer += Time.deltaTime;
            }
            else
            {
                ShowEffects(false);
                travelTimer = 0;
            }
        }
    }

    public void StopWelding(bool resetTimers = true)
    {
        if (resetTimers)
        {
            holdOn = false;
            weldTimer = 0;
        }

        ShowEffects(false);
        ResetBlobSettings(true);
    }

    private void ShowBlob()
    {
        float blobInitSize = 0.2f;

        if (!hasBlob)
        {
            if (weldHit.transform.gameObject.layer == 7) // Panel
            {
                currentPanel = weldHit.transform;

                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, weldHit.normal);
                currentBlob = Instantiate(weldBlobSet, weldHit.point, rotation);
                currentBlob.localScale = Vector3.one * blobInitSize;

                BlobThickness(currentBlob);
                SetBlobTravelTime(weldHit.transform.parent.GetComponent<WeldingPanel>());

                // ******** REAL-TIME PROGRESS UPDATE ********
                if (panel != null)
                {
                    float coverage = panel.GetCurrentCoverage();
                    if (panel.weldingProgressSlider != null)
                        panel.weldingProgressSlider.value = coverage;

                    if (panel.weldingProgressText != null)
                        panel.weldingProgressText.text = Mathf.RoundToInt(coverage * 100f) + "%";
                }
            }
            else if (weldHit.transform.gameObject.layer == 6) // Existing blob
            {
                currentBlob = weldHit.transform;

                currentBlob.parent = null;
                blobInitSize = currentBlob.localScale.x;

                currentBlob.GetComponent<WeldingBlobSet>().ShowGlow();

                // ******** REAL-TIME PROGRESS UPDATE ********
                if (panel != null)
                {
                    float coverage = panel.GetCurrentCoverage();
                    if (panel.weldingProgressSlider != null)
                        panel.weldingProgressSlider.value = coverage;

                    if (panel.weldingProgressText != null)
                        panel.weldingProgressText.text = Mathf.RoundToInt(coverage * 100f) + "%";
                }
            }

            hasBlob = true;
            blobSizeTimer = 0;
        }

        // Continue blob sizing
        if (hasBlob)
        {
            blobSizeTimer += Time.deltaTime * 0.2f;

            if (weldHit.transform == currentBlob)
            {
                if (currentBlob.localScale.magnitude < 0.7f)
                {
                    currentBlob.localScale = Vector3.one * (blobInitSize + blobSizeTimer);
                    BlobThickness(currentBlob);
                }
                else
                {
                    InstansiateHoleMask(currentBlob);
                    StopWelding(false);
                    StartCoroutine(HoldWeldingRoutine(0.5f));
                }
            }
            else
            {
                ResetBlobSettings();
            }
        }
    }

    private void BlobThickness(Transform blob)
    {
        if (isCornerWeld)
            blob.localScale = new Vector3(blob.localScale.x, blob.localScale.y * 1.3f, blob.localScale.z * 1.3f);
        else
            blob.localScale = new Vector3(blob.localScale.x, blob.localScale.y / 3, blob.localScale.z);
    }

    private void ResetBlobSettings(bool weldStop = false)
    {
        if (currentBlob)
        {
            currentBlob.parent = currentPanel;

            if (previousBlob)
            {
                previousBlob.LookAt(currentBlob, previousBlob.up);
                previousBlob.GetComponent<WeldingBlobSet>().tiltForward = true;
            }

            previousBlob = currentBlob;
        }

        if (weldStop)
        {
            if (previousBlob)
                previousBlob.GetComponent<WeldingBlobSet>().tiltForward = false;

            currentPanel = null;
            currentBlob = null;
            previousBlob = null;
        }

        hasBlob = false;
        blobSizeTimer = 0;
    }

    private IEnumerator HoldWeldingRoutine(float duration)
    {
        holdOn = true;
        yield return new WaitForSeconds(duration);
        holdOn = false;
    }

    private void InstansiateHoleMask(Transform blob)
    {
        Transform holeMask = Instantiate(weldHoleMask, blob.position, blob.rotation);
        holeMask.localScale = Vector3.one * 0.2f;

        float width = holeMask.localScale.x + 0.4f;
        Vector3 finalScale = new Vector3(width, holeMask.localScale.y + 0.7f, width);

        LeanTween.scale(holeMask.gameObject, finalScale, 0.2f);
        Destroy(blob.gameObject);
    }

    private void SetBlobTravelTime(WeldingPanel panel)
    {
        if (panel && travelTimer > 0)
        {
            panel.AddWeldTravel(travelTimer);
            travelTimer = 0;
        }
    }

    private void ShowEffects(bool show)
    {
        glowEffect.SetActive(show);

        if (show && !audioSource.isPlaying)
        {
            tipRenderer.material = weldBlobSet.GetComponent<WeldingBlobSet>().blobHotMaterial;
            audioSource.Play();
        }
        else if (!show && audioSource.isPlaying)
        {
            tipRenderer.material = tipOriginalMat;
            audioSource.Stop();
        }
    }

    public bool isCornerWeld = false;

    public Vector3 GetWeldPoint()
    {
        Vector3 weldPoint = weldingTip.position;

        if (Physics.Raycast(weldingTip.position, weldingTip.forward, out RaycastHit hit))
        {
            if (hit.transform.gameObject.layer == 6 || hit.transform.gameObject.layer == 7)
            {
                weldHit = hit;
                isWeldingLayer = true;
            }
            else
            {
                weldHit = new RaycastHit();
                isWeldingLayer = false;
            }

            weldPoint = hit.point;
            Debug.DrawLine(weldingTip.position, hit.point, Color.red);
        }

        return weldPoint;
    }

    public void SetTipRotation(Quaternion rotation)
    {
        weldingTip.rotation = rotation;
    }

    public void MoveHandle(Vector3 movePos)
    {
        transform.position = movePos;
    }
}
