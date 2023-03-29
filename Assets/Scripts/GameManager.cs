using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    // Singleton
    public static GameManager Instance { get; private set; }

    // Toggle for different modes
    public bool isLogging = false;
    public bool isDebugMode = false;
    public bool isReplay = false;
    public string replayFileName;

    // Oculus inputs
    public GameObject cameraRig;
    public GameObject head;
    public GameObject leftHand;
    public GameObject rightHand;
    public GameObject leftEye;
    public GameObject rightEye;

    // Display wall for stats
    public GameObject loggingText;
    public GameObject gameText;
    public GameObject replayText;
    public GameObject cubeBin;
    public GameObject sphereBin;
    public GameObject table;

    // Game settings
    public GameObject leftController;
    public GameObject rightController;
    public GameObject cube;
    public GameObject sphere;

    public float readsPerSecond = 2;
    public float replayLength = 30;
    public GameObject replayGroup;

    public GameObject cloneParent;
    public float gravity = 1.0f;
    public float clickRate = 0.2f;
    public float boxHeight = 1.0f;
    public float tableHeight = 1.5f;
    public Vector3 boxLocation = new Vector3(1, 0, 0);
    public Vector3 tableLocation = new Vector3(0, 1, 0.75f);
    public Vector3 spawnPoint;


    // OVRHand components
    private OVRHand leftOVRHand;
    private OVRHand rightOVRHand;
    private SkinnedMeshRenderer leftSkr;
    private SkinnedMeshRenderer rightSkr;

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
    private int replayCounter = 3;

    // IO variables
    private string path;
    private StreamWriter sw;
    private int curLine = 0;
    private double numLines;
    private List<string> replayData = new List<string>();
    private float startTime = 0;

    // Data tracking & Replay variables
    private List<GameObject> dataTargetList = new List<GameObject>();
    private List<OVRBone> dataTargetBoneList = new List<OVRBone>();
    private List<GameObject> replayTargetList = new List<GameObject>();
    private List<OVRBone> replayTargetBoneList = new List<OVRBone>();

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

        // initialize object locations
        cubeBin.transform.localPosition = boxLocation;
        cubeBin.transform.localScale = new Vector3(1, boxHeight, 1);
        sphereBin.transform.localPosition = new Vector3(-boxLocation.x, boxLocation.y, boxLocation.z);
        sphereBin.transform.localScale = new Vector3(1, boxHeight, 1);
        table.transform.localPosition = tableLocation;
        table.transform.localScale = new Vector3(1, tableHeight, 1);


        // initialize input components
        leftOVRHand = leftHand.GetComponent<OVRHand>();
        rightOVRHand = rightHand.GetComponent<OVRHand>();
        leftSkr = leftHand.GetComponent<SkinnedMeshRenderer>();
        rightSkr = rightHand.GetComponent<SkinnedMeshRenderer>();

        leftOVRSkeleton = leftHand.GetComponent<OVRSkeleton>();
        rightOVRSkeleton = rightHand.GetComponent<OVRSkeleton>();

        leftOVREyeGaze = leftEye.GetComponent<OVREyeGaze>();
        rightOVREyeGaze = rightEye.GetComponent<OVREyeGaze>();

        // exit if replaying, start text countdown and replay
        if (isReplay)
        {
            cameraRig.transform.position = new Vector3(0, 2, -3);
            leftController.SetActive(false);
            rightController.SetActive(false);
            replayText.SetActive(true);

            Invoke("ReadData", 0);
            return;
        }

        // initialize game variables
        Physics.gravity = new Vector3(0, -1 * gravity, 0);

        // initialize info for stats wall
        wallText = new List<TMP_Text>();
        if (isDebugMode)
        {
            loggingText.SetActive(true);
            
            for (int i = 0; i < loggingText.transform.childCount; i++)
            {
                wallText.Add(loggingText.transform.GetChild(i).GetComponent<TMP_Text>());
            }
        } else
        {
            gameText.SetActive(true);
            for (int i = 0; i < gameText.transform.childCount; i++)
            {
                wallText.Add(gameText.transform.GetChild(i).GetComponent<TMP_Text>());
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (isReplay && startTime == 0 && (leftOVRHand.GetFingerPinchStrength(OVRHand.HandFinger.Index) > 0.9f || rightOVRHand.GetFingerPinchStrength(OVRHand.HandFinger.Index) > 0.9f))
        {
            startTime = Time.time;
            Invoke("ReplayCountdown", 0f);
            Invoke("ReplayCountdown", 1.0f);
            Invoke("ReplayCountdown", 2.0f);
            Invoke("ReplayCountdown", 3.0f);
            InvokeRepeating("ReplayData", 3.0f, 1 / readsPerSecond);
            return;
        }

        if (isReplay)
        {
            return;
        }

        // begin experience & logging
        if (startTime == 0 && (leftOVRHand.GetFingerPinchStrength(OVRHand.HandFinger.Index) > 0.9f || rightOVRHand.GetFingerPinchStrength(OVRHand.HandFinger.Index) > 0.9f))
        {
            startTime = Time.time;
            SpawnShape("");

            if (isLogging)
            {
            string dataName = "data_v1_";
            string timeName = DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss", System.Globalization.CultureInfo.InvariantCulture);

            path = @"C:\Users\jonny\VR Authentication\Assets\Data\" + dataName + timeName + ".txt";

            if (!File.Exists(path))
            {
                using (FileStream fs = File.Create(path))
                {
                    fs.Close();
                }

            }
            InitializeData();
            InvokeRepeating("WriteData", 0, 1 / readsPerSecond);
            }
        }

        // change controller display if needed
        bool handsActive = (leftSkr.enabled && leftSkr.sharedMesh != null) || (rightSkr.enabled && rightSkr.sharedMesh != null);
        if (handsActive)
        {
            if (leftController.GetComponent<MeshRenderer>().enabled == true)
            {
                leftController.GetComponent<MeshRenderer>().enabled = false;
                rightController.GetComponent<MeshRenderer>().enabled = false;
            }
        }
        else
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
        if (isDebugMode)
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

            wallText[9].text = "Time: " + (Time.time-startTime).ToString("G4");
            wallText[10].text = "Score: " + score.ToString();
        } else
        {
            if (startTime == 0)
            {
                wallText[1].text = "Pinch Index/Thumb together to begin!";
                wallText[2].text = "";
            } else
            {
                wallText[1].text = "Time: " + (Time.time - startTime).ToString("G4");
                wallText[2].text = "Score: " + score.ToString();
            }
        }
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

    void InitializeData()
    {
        dataTargetList = new List<GameObject> { head, leftHand, rightHand, leftEye, rightEye };
        
        using (StreamWriter sw = File.AppendText(path))
        {
            List<string> headers = new List<string>{ "head","leftHand","rightHand","leftEye","rightEye" };
            for (int i = (int)leftOVRSkeleton.GetCurrentStartBoneId(); i < (int)leftOVRSkeleton.GetCurrentEndBoneId(); i++)
            {
                dataTargetBoneList.Add(leftOVRSkeleton.Bones[i]);
                headers.Add("left_"+OVRSkeleton.BoneLabelFromBoneId(OVRSkeleton.SkeletonType.HandLeft, leftOVRSkeleton.Bones[i].Id));
            }
            for (int i = (int)rightOVRSkeleton.GetCurrentStartBoneId(); i < (int)rightOVRSkeleton.GetCurrentEndBoneId(); i++)
            {
                dataTargetBoneList.Add(rightOVRSkeleton.Bones[i]);
                headers.Add("right_" + OVRSkeleton.BoneLabelFromBoneId(OVRSkeleton.SkeletonType.HandRight, rightOVRSkeleton.Bones[i].Id));
            }

            List<string> newHeaders = new List<string> {"time"};
            for (int i = 0; i < headers.Count; i++)
            {
                string h = headers[i];
                newHeaders.Add(h + "_position_x");
                newHeaders.Add(h + "_position_y");
                newHeaders.Add(h + "_position_z");
                newHeaders.Add(h + "_quaternion_w");
                newHeaders.Add(h + "_quaternion_x");
                newHeaders.Add(h + "_quaternion_y");
                newHeaders.Add(h + "_quaternion_z");
            }
            string line = String.Join(",", newHeaders);
            sw.WriteLine(line);
            sw.Close();
        }
    }
    
    void WriteData()
    {
        using (StreamWriter sw = File.AppendText(path))
        {
            string line = "";

            line += (Time.time-startTime);

            for (int i = 0; i < dataTargetList.Count; i++)
            {
                line += ParseV3(dataTargetList[i]);
            }

            for (int i = 0; i < dataTargetBoneList.Count; i++)
            {
                line += ParseV3OVRBone(dataTargetBoneList[i]);
            }

            sw.WriteLine(line);
            sw.Close();
        }

    }

    void ReadData()
    {
        path = @"C:\Users\jonny\VR Authentication\Assets\Data\"+replayFileName;
        using (StreamReader reader = new StreamReader(new FileStream(path, FileMode.Open)))
        {
            string line;
            // Read line by line  
            while ((line = reader.ReadLine()) != null)
            {
                numLines += 1;
                replayData.Add(line);
            }
        }
    }

    void ReplayData()
    {
        if (curLine == 0)
        {
            curLine++;
            for (int i = 0; i < replayGroup.transform.childCount; i++)
            {
                replayTargetList.Add(replayGroup.transform.GetChild(i).gameObject);
            }

            //OVRSkeleton leftReplayHand = replayGroup.transform.GetChild(1).gameObject.GetComponent<OVRSkeleton>();
            OVRSkeleton leftReplayHand = replayGroup.transform.GetChild(1).GetChild(1).gameObject.GetComponent<OVRSkeleton>();
            for (int i = (int)leftReplayHand.GetCurrentStartBoneId(); i < (int)leftReplayHand.GetCurrentEndBoneId(); i++)
            {
                replayTargetBoneList.Add(leftReplayHand.Bones[i]);
            }

            //OVRSkeleton rightReplayHand = replayGroup.transform.GetChild(2).gameObject.GetComponent<OVRSkeleton>();
            OVRSkeleton rightReplayHand = replayGroup.transform.GetChild(2).GetChild(1).gameObject.GetComponent<OVRSkeleton>();
            for (int i = (int)rightReplayHand.GetCurrentStartBoneId(); i < (int)rightReplayHand.GetCurrentEndBoneId(); i++)
            {
                replayTargetBoneList.Add(rightReplayHand.Bones[i]);
            }
        }

        if (curLine < numLines)
        {
            Debug.Log("Currently at " + (Time.time - startTime));
            var replayValues = replayData[curLine].Split(",").Where(x => x != "").ToList();
            Debug.Log(replayData[curLine]);
            curLine++;

            if (float.Parse(replayValues[0]) > replayLength)
            {
                curLine = 10000000;
                ReplayCountdown();
                return;
            }

            for (int i = 0; i < replayTargetList.Count; i++)
            {
                int start = 7 * i + 1;

                // left eye
                if (i == 3)
                {
                    var hp = new Vector3(float.Parse(replayValues[1]), float.Parse(replayValues[2]), float.Parse(replayValues[3]));
                    var leftEyeVector = new Quaternion(float.Parse(replayValues[start + 3]), float.Parse(replayValues[start + 4]), float.Parse(replayValues[start + 5]), float.Parse(replayValues[start + 6])) * Vector3.forward;
                    RaycastHit leftEyeHit;
                    if (Physics.Raycast(hp, leftEyeVector, out leftEyeHit))
                    {
                        replayGroup.transform.GetChild(3).position = hp + new Vector3(0.1f, 0, 0);

                        LineRenderer lr = replayGroup.transform.GetChild(3).GetComponent<LineRenderer>();
                        lr.SetPosition(0, hp + new Vector3(0.5f, 0, 0));
                        lr.SetPosition(1, leftEyeHit.point);
                    }
                    continue;
                }

                // right eye
                if (i == 4)
                {
                    var hp = new Vector3(float.Parse(replayValues[1]), float.Parse(replayValues[2]), float.Parse(replayValues[3]));
                    var rightEyeVector = new Quaternion(float.Parse(replayValues[start + 3]), float.Parse(replayValues[start + 4]), float.Parse(replayValues[start + 5]), float.Parse(replayValues[start + 6])) * Vector3.forward;
                    RaycastHit rightEyeHit;
                    if (Physics.Raycast(hp, rightEyeVector, out rightEyeHit))
                    {
                        replayGroup.transform.GetChild(4).position = hp + new Vector3(-0.1f, 0, 0);

                        LineRenderer lr = replayGroup.transform.GetChild(4).GetComponent<LineRenderer>();
                        lr.SetPosition(0, hp + new Vector3(-0.5f, 0, 0));
                        lr.SetPosition(1, rightEyeHit.point);
                    }
                    continue;
                }

                replayTargetList[i].transform.position = new Vector3(float.Parse(replayValues[start]), float.Parse(replayValues[start+1]), float.Parse(replayValues[start+2]));
                replayTargetList[i].transform.rotation = new Quaternion(float.Parse(replayValues[start + 3]), float.Parse(replayValues[start + 4]), float.Parse(replayValues[start + 5]), float.Parse(replayValues[start + 6]));
            }

            for (int i = 0; i < replayTargetBoneList.Count; i++)
            {
                int start = 7 * (i + replayTargetList.Count) + 1;

                replayTargetBoneList[i].Transform.position = new Vector3(float.Parse(replayValues[start]), float.Parse(replayValues[start + 1]), float.Parse(replayValues[start + 2]));
                replayTargetBoneList[i].Transform.rotation = new Quaternion(float.Parse(replayValues[start + 3]), float.Parse(replayValues[start + 4]), float.Parse(replayValues[start + 5]), float.Parse(replayValues[start + 6]));
            }
        } else
        {
            ReplayCountdown();
        }
    }

    void ReplayCountdown()
    {
        replayGroup.SetActive(true);
        if (replayCounter > 0)
        {
            replayText.transform.GetChild(0).GetComponent<TMP_Text>().text = "Replay beginning in " + replayCounter.ToString(); 
        } else if (replayCounter == 0)
        {
            replayText.transform.GetChild(0).GetComponent<TMP_Text>().text = "Replay in progress";
        } else
        {
            replayText.transform.GetChild(0).GetComponent<TMP_Text>().text = "Replay finished";
        }

        replayCounter -= 1;
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

    private string ParseV3(GameObject gameObject)
    {
        string res = "";
        string v3String = gameObject.transform.position.ToString() + gameObject.transform.rotation.ToString();
        char[] delimiters = { ' ', ',', '(', ')' };
        List<string> nums = v3String.Split(delimiters).Where(x => x != "").ToList();
        for (int i = 0; i < nums.Count; i++)
        {
            res += ","+nums[i];
        }

        return res;
    }

    private string ParseV3OVRBone(OVRBone ovrBone)
    {
        //Debug.Log(ovrBone.Transform.localScale);
        string res = "";
        string v3String = ovrBone.Transform.position.ToString() + ovrBone.Transform.rotation.ToString();
        char[] delimiters = { ' ', ',', '(', ')' };
        List<string> nums = v3String.Split(delimiters).Where(x => x != "").ToList();
        for (int i = 0; i < nums.Count; i++)
        {
            res += "," + nums[i];
        }

        return res;
    }
}
