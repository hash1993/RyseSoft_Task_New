using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using static WeldingPanel;
using TMPro;
using UnityEngine.UI;

public class WeldingPanel : MonoBehaviour
{
    [SerializeField] private Collider weldingCollider;

    [SerializeField] private Transform[] panels;

    [SerializeField] private Material blobErrorMat, blobGoodMat;

    [SerializeField] private GameObject weldScanner;
    [SerializeField] private int checkTimeSec = 2;
    [SerializeField] private Transform[] checkingTransforms;
   public UnityEngine.UI.Slider weldingProgressSlider;
   public UnityEngine.UI.Text weldingProgressText;
     public GameObject finishedPanelObject;
    public GameObject inventoryImage;

    public GameObject[] partInventoryIcons;   // multiple icons
    public Text[] freeSlotTexts; 
    private Transform checkerCapsule;
    private WeldCheckerLight checkerLight;
    private Vector3[] checkingPoints;

    public struct WeldingStats
    {
        public float uniformity;
        public float coveragePercent;
        public float travel;

        public int badweldCount;
        public int holesCount;

    }

       

 public void InitializePanel()
    {
        if (partInventoryIcons != null)
        {
            foreach (GameObject icon in partInventoryIcons)
                if (icon != null) icon.SetActive(true);
        }

        if (freeSlotTexts != null)
        {
            foreach (Text t in freeSlotTexts)
                if (t != null) t.gameObject.SetActive(false);
        }

        if (finishedPanelObject != null)
            finishedPanelObject.SetActive(false);
    }

    // Called when a part is welded
    // partIndex = index of the welded part in the arrays
    public void CompletePart(int partIndex)
    {
        if (partInventoryIcons != null && partIndex < partInventoryIcons.Length && partInventoryIcons[partIndex] != null)
            partInventoryIcons[partIndex].SetActive(false); // Remove icon

        if (freeSlotTexts != null && partIndex < freeSlotTexts.Length && freeSlotTexts[partIndex] != null)
            freeSlotTexts[partIndex].gameObject.SetActive(true); // Show FREE SLOT text
    }

    // Called when whole panel is completed
    public void CompletePanel()
    {
        if (finishedPanelObject != null)
            finishedPanelObject.SetActive(true);

        // Hide all remaining icons and show their free slot texts
        if (partInventoryIcons != null && freeSlotTexts != null)
        {
            for (int i = 0; i < partInventoryIcons.Length; i++)
            {
                if (partInventoryIcons[i] != null) partInventoryIcons[i].SetActive(false);
                if (freeSlotTexts[i] != null) freeSlotTexts[i].gameObject.SetActive(true);
            }
        }
    }
    void Awake()
    {
        checkingPoints = new Vector3[checkingTransforms.Length];

        int i = 0;
        foreach (Transform t in checkingTransforms)
        {
            checkingPoints[i] = t.position;
            i++;
        }

    }


    bool isWeldingStatsDone = false;
    WeldingStats weldingStats;
    internal void PopulateWeldingStats(out int delayTimeSec)
    {
        
   

        delayTimeSec = checkTimeSec;

        isWeldingStatsDone = false;

        weldingStats = new WeldingStats();

        if (checkerCapsule == null)
            checkerCapsule = Instantiate(weldScanner, checkingPoints[0], Quaternion.identity).transform;

        if (checkerLight == null)
            checkerLight = checkerCapsule.GetComponent<WeldCheckerLight>();

        checkerCapsule.rotation = checkingTransforms[0].rotation; //Match rotation in case of corner welds needs a bit of tilt.

        weldingStats.uniformity = GetUniformity();
        weldingStats.travel = GetWeldTravelUniformity();

        weldingStats.badweldCount = GetBadWelds();
        weldingStats.holesCount = GetWeldHoles();

        int totalCount = 0;
        int blobCount = 0;

   LeanTween.move(checkerCapsule.gameObject, checkingPoints, checkTimeSec)
    .setOnUpdate((Vector3 positionValue) =>
    {
        bool hasBlob = RaycastCheckWeld(checkerCapsule);
        totalCount++;

        if (hasBlob)
        {
            blobCount++;
            checkerLight.ShowColor(true);
            checkerCapsule.GetComponent<AudioSource>().pitch = 1f;
        }
        else
        {
            checkerCapsule.GetComponent<AudioSource>().pitch = 1.3f;
            checkerLight.ShowColor(false);
        }
    })
    .setOnComplete(() =>
    {
        weldingStats.coveragePercent = (float)blobCount / (float)totalCount;
        isWeldingStatsDone = true;

        

        Destroy(checkerCapsule.gameObject);
    });



    }

    internal bool GetWeldResults(out WeldingStats stats)
    {
        stats = weldingStats;
        return isWeldingStatsDone;
    }

    private bool RaycastCheckWeld(Transform checkPos)
    {
        bool hasBlob = false;

        Vector3 checkPosWithGap = checkPos.position + Vector3.up * 0.1f;

        if (Physics.Raycast(checkPosWithGap, Vector3.down, out RaycastHit hit))
        {
            if (hit.transform.gameObject.layer == 6) //Hits welding blob.
            {
                hasBlob = true;
                //Debug.DrawRay(checkPosWithGap, Vector3.down, Color.green, 100);
            }
            else
            {
                hasBlob = false;
                //Debug.DrawRay(checkPosWithGap, Vector3.down, Color.red, 100);
            }

        }

        return hasBlob;
    }

    //Blobs not in contact with welding line.
    private int GetBadWelds()
    {
        int badWeldsCount = 0;

        foreach (Transform panel in panels)
        {
            WeldingBlobSet[] blobs = panel.GetComponentsInChildren<WeldingBlobSet>();

            foreach (WeldingBlobSet blob in blobs)
            {
                //Change to Weld Panel Layer, to not get counted by coverage detection.
                blob.gameObject.layer = 7; 

                //Delay change color for effect
                LeanTween.value(0, 1, checkTimeSec).setOnComplete(() =>
                {
                    blob.GetComponent<Renderer>().material = blobErrorMat;
                });
            }


            badWeldsCount += blobs.Length;
        }

        //Good welds
        WeldingBlobSet[] goodBlobs = weldingCollider.transform.GetComponentsInChildren<WeldingBlobSet>();

        foreach (WeldingBlobSet blob in goodBlobs)
        {
            //Delay change color for effect
            LeanTween.value(0, 1, checkTimeSec).setOnComplete(() =>
            {
                blob.GetComponent<Renderer>().material = blobGoodMat;
            });
        }

        return badWeldsCount;
    }

    private int GetWeldHoles()
    {
       
        GameObject[] holeObjects = GameObject.FindGameObjectsWithTag("WeldHole");
        int holesCount = holeObjects.Length;

        return holesCount;

    }

    private float GetUniformity()
    {
        float uniformity = 0.0f;

        float smallestScale = Mathf.Infinity;
        float largestScale = 0;

        GameObject[] weldObjects = GameObject.FindGameObjectsWithTag("WeldObject");
        foreach (GameObject obj in weldObjects)
        {
            if(obj.transform.localScale.x < smallestScale)
                smallestScale = obj.transform.localScale.x;

            if(obj.transform.localScale.x > largestScale)
                largestScale = obj.transform.localScale.x;


        }

        uniformity = ((smallestScale + largestScale) / 2)/largestScale;

        return uniformity;
    }

    //Weld Travel
    List<float> weldTravels = new List<float>();
    internal void AddWeldTravel(float weldTravel)
    {
        weldTravels.Add(weldTravel);
    }
    internal void ResetWeldTravel()
    {
        weldTravels.Clear();
    }

    private float GetWeldTravelUniformity()
    {
        if (weldTravels.Count <= 10)
            return 0;


        float idealTime = 0.419f; //Ideal time for each blob to form before making another.

        float averageTime = weldTravels.Average();

        float travelPerf = 1 - (Mathf.Abs(idealTime - averageTime) / idealTime);


        //Debug.Log("GetWeldTravelPerformance: averageTime = " + averageTime);

        return travelPerf;
    }
public float GetCurrentCoverage()
{
    int totalCount = checkingTransforms.Length;
    int blobCount = 0;

    foreach (Transform check in checkingTransforms)
    {
        Vector3 checkPosWithGap = check.position + Vector3.up * 0.1f;

        if (Physics.Raycast(checkPosWithGap, Vector3.down, out RaycastHit hit))
        {
            if (hit.transform.gameObject.layer == 6) // Only count welding blobs
            {
                blobCount++;
            }
        }
    }

    float coverage = Mathf.Clamp01((float)blobCount / (float)totalCount);

    // Update slider if assigned
    if (weldingProgressSlider != null)
        weldingProgressSlider.value = coverage;

    // Update percentage text if assigned
    if (weldingProgressText != null)
        weldingProgressText.text = Mathf.RoundToInt(coverage * 100f) + "%";

    return coverage;
}


}
