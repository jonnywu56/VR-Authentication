using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    // Singleton
    public static GameManager Instance { get; private set; }

    // Oculus inputs
    public GameObject head;
    public GameObject leftHand;
    public GameObject rightHand;
    public GameObject leftEye;
    public GameObject rightEye;
    

    // Display wall for stats
    public GameObject displayWall;

    // Toggle for logging to txt file
    public bool isLogging = false;

    // Game settings
    public GameObject leftController;
    public GameObject rightController;
    public GameObject cube;
    public GameObject sphere;
    public Vector3 spawnPoint;
    public GameObject cloneParent;
    public float gravity = 1.0f;
    public float clickRate = 0.2f;

    // OVRHand components
    private OVRHand leftOVRHand;
    private OVRHand rightOVRHand;
    private SkinnedMeshRenderer skr;

    // OVRSkeleton components
    private OVRSkeleton leftOVRSkeleton;
    private OVRSkeleton rightOVRSkeleton;

    // OVREyeGaze components
    private OVREyeGaze leftOVREyeGaze;
    private OVREyeGaze rightOVREyeGaze;

    // Game variables
    private List<TMP_Text> wallText;
    private static int score = 0;
    private float nextClick = 0f;
    private bool leftHandGrabbed = false;
    private bool rightHandGrabbed = false;

    // IO variables
    private string path;
    private StreamWriter sw;

    // Start is called before the first frame update
    void Start()
    {
        // singleton logic
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        } else {
            Destroy(gameObject);
        }
        // initialize game variables
        Physics.gravity = new Vector3(0, -1 * gravity, 0);
        SpawnShape("");

        // initialize input components
        leftOVRHand = leftHand.GetComponent<OVRHand>();
        rightOVRHand = rightHand.GetComponent<OVRHand>();
        skr = leftHand.GetComponent<SkinnedMeshRenderer>();

        leftOVRSkeleton = leftHand.GetComponent<OVRSkeleton>();
        rightOVRSkeleton = rightHand.GetComponent<OVRSkeleton>(); 

        leftOVREyeGaze = leftEye.GetComponent<OVREyeGaze>();
        rightOVREyeGaze = rightEye.GetComponent<OVREyeGaze>();

        // initialize info for stats wall
        wallText = new List<TMP_Text>();
        for (int i = 0; i < displayWall.transform.childCount; i++)
        {
            wallText.Add(displayWall.transform.GetChild(i).GetComponent<TMP_Text>());
        }

        // start writing to output file
        string dataName = "data_v1_";
        string timeName = DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss", System.Globalization.CultureInfo.InvariantCulture);
        float readsPerSecond = 2;

        if (isLogging)
        {
            path = @"C:\Users\jonny\VR Authentication\Assets\Data\" + dataName + timeName + ".txt";

            if (!File.Exists(path))
            {
                using (FileStream fs = File.Create(path))
                {
                    fs.Close();
                }

            }
            InvokeRepeating("WriteData", 0, 1 / readsPerSecond);
        }
    }


    // Update is called once per frame
    void Update()
    {
        /**
        bool isIndexFingerPinching = rightOVRHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        if (isIndexFingerPinching)
        {
            Debug.Log("CUUUBE");
            Instantiate(cube, new Vector3(0, 0, 4), Quaternion.identity);
            Debug.Log(leftEye.transform.rotation);
            Debug.Log(rightEye.transform.rotation);
            return;
            for (int i = (int) leftOVRSkeleton.GetCurrentStartBoneId(); i < (int) leftOVRSkeleton.GetCurrentEndBoneId(); i++)
            {
                OVRBone target = leftOVRSkeleton.Bones[i];
                Debug.Log(target.Transform);
                Debug.Log(target.Transform.position);
            }
        }
        **/

        bool handsActive = skr.enabled && skr.sharedMesh != null;

        // spawn cubes (debugging)
        if (OVRInput.Get(OVRInput.RawButton.X) && !handsActive && Time.time > nextClick)
        {
            nextClick = Time.time + clickRate;
            SpawnShape("");
        }

        // change controller display if needed
        if (handsActive)
        {
            if (leftController.GetComponent<MeshRenderer>().enabled == true)
            {
                leftController.GetComponent<MeshRenderer>().enabled = false;
                rightController.GetComponent<MeshRenderer>().enabled = false;
            }
        } else
        {
            if (leftController.GetComponent<MeshRenderer>().enabled == false)
            {
                leftController.GetComponent<MeshRenderer>().enabled = true;
                rightController.GetComponent<MeshRenderer>().enabled = true;
            }
        }

        UpdateWall();
    }

    // Update text on display wall
    void UpdateWall()
    {
        wallText[0].text = "Head Position: " + head.transform.position.ToString();
        wallText[1].text = "Left Hand Position: " + leftHand.transform.position.ToString();
        wallText[2].text = "Right Hand Position: " + rightHand.transform.position.ToString();

        wallText[3].text = "Left Eye Rotation: " + leftEye.transform.eulerAngles.ToString();
        wallText[4].text = "Right Eye Rotation: " + rightEye.transform.eulerAngles.ToString();

        wallText[5].text = "Head Rotation: " + head.transform.eulerAngles.ToString();
        wallText[6].text = "Left Hand Rotation: " + leftHand.transform.eulerAngles.ToString();
        wallText[7].text = "Right Hand Rotation: " + rightHand.transform.eulerAngles.ToString();

        wallText[8].text = "Hands Active? " + (leftHand.GetComponent<SkinnedMeshRenderer>().enabled && leftHand.GetComponent<SkinnedMeshRenderer>().sharedMesh != null).ToString();

        wallText[9].text = "Time: " + Time.time.ToString("G2");
        wallText[10].text = "Score: " + score.ToString();
    }

    // Create shape
    public void SpawnShape(string shapeName)
    {
        if (shapeName == "cube")
        { 
            Instantiate(cube, spawnPoint, Quaternion.identity, cloneParent.transform); 
        } else if (shapeName == "sphere")
        {
            Instantiate(sphere, spawnPoint, Quaternion.identity, cloneParent.transform);
        } else 
        {
            if (UnityEngine.Random.Range(0, 2) == 0)
            {
                Instantiate(cube, spawnPoint, Quaternion.identity, cloneParent.transform);
            }
            else
            {
                Instantiate(sphere, spawnPoint, Quaternion.identity, cloneParent.transform);
            }
        }

        // Delete oldest shape if too many exist
        if (cloneParent.transform.childCount > 10)
        {
            PickUpObject pu = cloneParent.transform.GetChild(0).gameObject.GetComponent<PickUpObject>();
            Debug.Log(pu.pickedUp);
            
            if (pu.pickedUp)
            {
                Debug.Log(pu.controller.gameObject.tag);
                if (pu.controller.gameObject.tag == "LeftHand")
                {
                    leftHandGrabbed = false;
                } else
                {
                    rightHandGrabbed = false;
                }
            }
            Destroy(cloneParent.transform.GetChild(0).gameObject);
        }
    }

    // Update is called once per frame
    void WriteData()
    {
        using (StreamWriter sw = File.AppendText(path))
        {
            sw.WriteLine(Time.time.ToString());
            sw.Close();
        }

    }

    public void IncreaseScore()
    {
        score += 1;
    }

    public void DecreaseScore()
    {
        score -= 1;
    }
    
    public bool CheckGrab(string hand)
    {
        if (hand == "left")
        {
            return leftHandGrabbed;
        }
        return rightHandGrabbed;
    }

    public void SetGrab(string hand, bool status)
    {
        if (hand == "left")
        {
            leftHandGrabbed = status;
        } else
        {
            rightHandGrabbed = status;
        }
    }
}
