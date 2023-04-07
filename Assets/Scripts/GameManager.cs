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
    [Serializable]
    public class ExperimentVars
    {
        public bool isLogging = false;
        public bool isDetailedMode = false;
        public string loggingFileName;
        public string replayFileName;

        public float readsPerSecond = 2;
        public float gameLength = 30;
        public float replayLength = 30;
        public GameObject replayGroup;
        public GameObject cloneParent;
    }
    public ExperimentVars experimentVars = new ExperimentVars();

    // Oculus inputs
    [Serializable]
    public class OculusInputs
    {
        public GameObject cameraRig;
        public GameObject head;
        public GameObject leftHand;
        public GameObject rightHand;
        public GameObject leftEye;
        public GameObject rightEye;
    }
    public OculusInputs oculusInputs = new OculusInputs();

    // Props
    [Serializable]
    public class Props
    {
        public GameObject gameText;
        public GameObject lobbyText;
        public GameObject loggingText;
        public GameObject replayText;
        public GameObject cubeBin;
        public GameObject sphereBin;
        public GameObject table;
        public GameObject leftController;
        public GameObject rightController;
        public GameObject cube;
        public GameObject sphere;
    }
    public Props props = new Props();


    [Serializable]
    public class GameVars
    {
        public float gravity = 1.0f;
        public float boxHeight = 1.0f;
        public float tableHeight = 0.1f;
        public Vector3 boxLocation = new Vector3(1, 0, 0);
        public Vector3 tableLocation = new Vector3(0, 1, 0.75f);
        public Vector3 spawnLocation = new Vector3(0, 2, 0.4f);
        public Vector3 cameraLocation = new Vector3(0, 2, 0);
    }
    public GameVars gameVars = new GameVars();


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

    // Game mode
    public enum GameMode
    {
        Lobby,
        Game,
        Replay
    }
    private GameMode gameMode = GameMode.Lobby;

    // Game variables
    private List<TMP_Text> wallText;
    private static int score = 0;
    private bool leftHandGrabbed = false;
    private bool rightHandGrabbed = false;
    private bool replayActive = false;
    private bool replayDone = false;
    private int shapeNum = 0;

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

        // initialize camera location
        oculusInputs.cameraRig.transform.position = gameVars.cameraLocation;

        // initialize object locations
        props.cubeBin.transform.localPosition = gameVars.boxLocation;
        props.cubeBin.transform.localScale = new Vector3(1, gameVars.boxHeight, 1);
        props.sphereBin.transform.localPosition = new Vector3(-gameVars.boxLocation.x, gameVars.boxLocation.y, gameVars.boxLocation.z);
        props.sphereBin.transform.localScale = new Vector3(1, gameVars.boxHeight, 1);
        props.table.transform.localPosition = gameVars.tableLocation;
        props.table.transform.localScale = new Vector3(1, gameVars.tableHeight, 1);
        


        // initialize input components
        leftOVRHand = oculusInputs.leftHand.GetComponent<OVRHand>();
        rightOVRHand = oculusInputs.rightHand.GetComponent<OVRHand>();
        leftSkr = oculusInputs.leftHand.GetComponent<SkinnedMeshRenderer>();
        rightSkr = oculusInputs.rightHand.GetComponent<SkinnedMeshRenderer>();

        leftOVRSkeleton = oculusInputs.leftHand.GetComponent<OVRSkeleton>();
        rightOVRSkeleton = oculusInputs.rightHand.GetComponent<OVRSkeleton>();

        leftOVREyeGaze = oculusInputs.leftEye.GetComponent<OVREyeGaze>();
        rightOVREyeGaze = oculusInputs.rightEye.GetComponent<OVREyeGaze>();

        // initialize game variables
        Physics.gravity = new Vector3(0, -1 * gameVars.gravity, 0);

        StartLobby();
    }

    public void StartGame()
    {
        gameMode = GameMode.Game;

        props.gameText.SetActive(!experimentVars.isDetailedMode);
        props.lobbyText.SetActive(false);
        props.loggingText.SetActive(experimentVars.isDetailedMode);
        props.replayText.SetActive(false);

        startTime = Time.time;
        score = 0;
        shapeNum = 0;
        SpawnShape();

        if (experimentVars.isLogging)
        {
            string dataName = experimentVars.loggingFileName;
            if (dataName == "") dataName = "data";
            string timeName = DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss", System.Globalization.CultureInfo.InvariantCulture);

            path = @"C:\Users\jonny\VR Authentication\Assets\Data\" + dataName + "_" + timeName + ".txt";
            

            if (!File.Exists(path))
            {
                using (FileStream fs = File.Create(path))
                {
                    fs.Close();
                }

            }
            InitializeData();
            InvokeRepeating("WriteData", 0, 1 / experimentVars.readsPerSecond);
        }
    }

    void UpdateGame()
    {
        float curTime = Time.time - startTime;

        // Update hand representation when controllers present
        bool handsActive = (leftSkr.enabled && leftSkr.sharedMesh != null) || (rightSkr.enabled && rightSkr.sharedMesh != null);
        if (handsActive)
        {
            if (props.leftController.GetComponent<MeshRenderer>().enabled == true)
            {
                props.leftController.GetComponent<MeshRenderer>().enabled = false;
                props.rightController.GetComponent<MeshRenderer>().enabled = false;
            }
        }
        else
        {
            if (props.leftController.GetComponent<MeshRenderer>().enabled == false)
            {
                props.leftController.GetComponent<MeshRenderer>().enabled = true;
                props.rightController.GetComponent<MeshRenderer>().enabled = true;
            }
        }

        // Update text on front wall
        if (!experimentVars.isDetailedMode)
        {
            props.gameText.transform.GetChild(1).GetComponent<TMP_Text>().text = "Time: " + (Time.time - startTime).ToString("G4");
            props.gameText.transform.GetChild(2).GetComponent<TMP_Text>().text = "Score: " + score.ToString();
        } else
        {
            props.loggingText.transform.GetChild(0).GetComponent<TMP_Text>().text = "Head Position: " + oculusInputs.head.transform.position.ToString();
            props.loggingText.transform.GetChild(1).GetComponent<TMP_Text>().text = "Left Hand Position: " + oculusInputs.leftHand.transform.position.ToString();
            props.loggingText.transform.GetChild(2).GetComponent<TMP_Text>().text = "Right Hand Position: " + oculusInputs.rightHand.transform.position.ToString();

            props.loggingText.transform.GetChild(3).GetComponent<TMP_Text>().text = "Left Eye Rotation: " + oculusInputs.leftEye.transform.eulerAngles.ToString();
            props.loggingText.transform.GetChild(4).GetComponent<TMP_Text>().text = "Right Eye Rotation: " + oculusInputs.rightEye.transform.eulerAngles.ToString();

            props.loggingText.transform.GetChild(5).GetComponent<TMP_Text>().text = "Head Rotation: " + oculusInputs.head.transform.eulerAngles.ToString();
            props.loggingText.transform.GetChild(6).GetComponent<TMP_Text>().text = "Left Hand Rotation: " + oculusInputs.leftHand.transform.eulerAngles.ToString();
            props.loggingText.transform.GetChild(7).GetComponent<TMP_Text>().text = "Right Hand Rotation: " + oculusInputs.rightHand.transform.eulerAngles.ToString();

            props.loggingText.transform.GetChild(8).GetComponent<TMP_Text>().text = "Hands Active? " + (oculusInputs.leftHand.GetComponent<SkinnedMeshRenderer>().enabled && oculusInputs.leftHand.GetComponent<SkinnedMeshRenderer>().sharedMesh != null).ToString();

            props.loggingText.transform.GetChild(9).GetComponent<TMP_Text>().text = "Time: " + (Time.time - startTime).ToString("G4");
            props.loggingText.transform.GetChild(10).GetComponent<TMP_Text>().text = "Score: " + score.ToString();
        }

        if (curTime > experimentVars.gameLength)
        {
            EndGame();
        }
        
    }

    void EndGame()
    {
        gameMode = GameMode.Lobby;

        SetGrab("left", false);
        SetGrab("right", false);
        Destroy(experimentVars.cloneParent.transform.GetChild(0).gameObject);
        CancelInvoke("WriteData");

        StartLobby();
    }

    void StartLobby()
    {
        gameMode = GameMode.Lobby;

        props.gameText.SetActive(false);
        props.lobbyText.SetActive(true);
        props.loggingText.SetActive(false);
        props.replayText.SetActive(false);

        for (int i = 0; i < props.table.transform.childCount; i++)
        {
            props.table.transform.GetChild(i).gameObject.SetActive(true);
        }
    }

    public void EndLobby()
    {
        for (int i = 0; i < props.table.transform.childCount; i++)
        {
            props.table.transform.GetChild(i).gameObject.SetActive(false);
        }
    }

    //start text countdown and replay
    public void StartReplay()
    {
        gameMode = GameMode.Replay;

        props.gameText.SetActive(false);
        props.lobbyText.SetActive(false);
        props.loggingText.SetActive(false);
        props.replayText.SetActive(true);

        experimentVars.replayGroup.SetActive(true);
        replayActive = false;
        curLine = 0;

        oculusInputs.cameraRig.transform.position = new Vector3(0, 2, -2);
        props.leftController.SetActive(false);
        props.rightController.SetActive(false);

        Invoke("ReadData", 0);

        startTime = Time.time;
    }

    void UpdateReplay()
    {
        float curTime = Time.time - startTime;

        if (curTime < 1.0)
        {
            props.replayText.transform.GetChild(0).GetComponent<TMP_Text>().text = "Replay beginning in 3";
            props.replayText.transform.GetChild(1).GetComponent<TMP_Text>().text = "";
            props.replayText.transform.GetChild(2).GetComponent<TMP_Text>().text = "";
            props.replayText.transform.GetChild(3).GetComponent<TMP_Text>().text = "";
        } else if (curTime < 2.0)
        {
            props.replayText.transform.GetChild(0).GetComponent<TMP_Text>().text = "Replay beginning in 2";
        } else if (curTime < 3.0)
        {
            props.replayText.transform.GetChild(0).GetComponent<TMP_Text>().text = "Replay beginning in 1";
        } else if (curTime >= 3 && curTime < 3 + experimentVars.replayLength && !replayActive)
        {
            replayActive = true;

            props.replayText.transform.GetChild(0).GetComponent<TMP_Text>().text = "Replay in progress";
            props.replayText.transform.GetChild(1).GetComponent<TMP_Text>().text = "Time: " + (Time.time - startTime).ToString("G4"); ;

            // enable eye rays
            experimentVars.replayGroup.transform.GetChild(4).gameObject.SetActive(true);
            experimentVars.replayGroup.transform.GetChild(5).gameObject.SetActive(true);

            InvokeRepeating("ReplayData", 0, 1 / experimentVars.readsPerSecond);
        } else if (curTime >= 3 && curTime < 3 + experimentVars.replayLength)
        {
            props.replayText.transform.GetChild(1).GetComponent<TMP_Text>().text = "Time: " + (Time.time - startTime - 3).ToString("G4"); ;
        } else if (curTime >= 3 + experimentVars.replayLength)
        {
            EndReplay();
        }
    }

    void EndReplay()
    {
        oculusInputs.cameraRig.transform.position = new Vector3(0, 2, 0);

        //disable eye rays
        experimentVars.replayGroup.SetActive(false);
        experimentVars.replayGroup.transform.GetChild(4).gameObject.SetActive(false);
        experimentVars.replayGroup.transform.GetChild(5).gameObject.SetActive(false);

        CancelInvoke("ReplayData");

        StartLobby();
    }

    // Update is called once per frame
    void Update()
    {
        if (gameMode == GameMode.Game)
        {
            UpdateGame();
        } else if (gameMode == GameMode.Replay)
        {
            UpdateReplay();
        }
    }
 
    // Create shape
    public void SpawnShape()
    {
        shapeNum = (shapeNum + 1) % 2;
        if (shapeNum == 0)
        {
            Instantiate(props.cube, gameVars.spawnLocation, Quaternion.identity, experimentVars.cloneParent.transform);
        } else
        {
            Instantiate(props.sphere, gameVars.spawnLocation, Quaternion.identity, experimentVars.cloneParent.transform);
        }
    }

    void InitializeData()
    {
        dataTargetList = new List<GameObject> { oculusInputs.head, oculusInputs.leftHand, oculusInputs.rightHand, oculusInputs.leftEye, oculusInputs.rightEye };
        dataTargetBoneList = new List<OVRBone>();
        
        using (StreamWriter sw = File.AppendText(path))
        {
            List<string> headers = new List<string>{"shape", "head","leftHand","rightHand","leftEye","rightEye" };
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

            List<string> newHeaders = new List<string> {"time", "score", "shapeNum"};
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
            line += "," + score;

            line += "," + shapeNum.ToString();

            GameObject curShape = experimentVars.cloneParent.transform.GetChild(0).gameObject;
            line += ParseV3(curShape);

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
        if (replayDone) return;

        path = @"C:\Users\jonny\VR Authentication\Assets\Data\"+experimentVars.replayFileName;
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
        if (!replayDone)
        {
            for (int i = 0; i < experimentVars.replayGroup.transform.childCount; i++)
            {
                replayTargetList.Add(experimentVars.replayGroup.transform.GetChild(i).gameObject);
            }

            OVRSkeleton leftReplayHand = experimentVars.replayGroup.transform.GetChild(2).GetChild(1).gameObject.GetComponent<OVRSkeleton>();
            
            for (int i = (int)leftReplayHand.GetCurrentStartBoneId(); i < (int)leftReplayHand.GetCurrentEndBoneId(); i++)
            {
                replayTargetBoneList.Add(leftReplayHand.Bones[i]);
            }

            OVRSkeleton rightReplayHand = experimentVars.replayGroup.transform.GetChild(3).GetChild(1).gameObject.GetComponent<OVRSkeleton>();
            for (int i = (int)rightReplayHand.GetCurrentStartBoneId(); i < (int)rightReplayHand.GetCurrentEndBoneId(); i++)
            {
                replayTargetBoneList.Add(rightReplayHand.Bones[i]);
            }

            replayDone = true;
        }

        if (curLine > 0 && curLine < numLines)
        {
            var replayValues = replayData[curLine].Split(",").Where(x => x != "").ToList();

            props.replayText.transform.GetChild(2).GetComponent<TMP_Text>().text = "Replay Time: " + float.Parse(replayValues[0]).ToString("G4");
            props.replayText.transform.GetChild(3).GetComponent<TMP_Text>().text = "Score: " + int.Parse(replayValues[1]);

            if (float.Parse(replayValues[0]) > experimentVars.replayLength)
            {
                EndReplay();
                return;
            }

            
            for (int i = 0; i < replayTargetList.Count; i++)
            {
                int start = 7 * i + 3;

                // shape
                if (i == 0)
                {
                    int curShapeNum = int.Parse(replayValues[2]);

                    experimentVars.replayGroup.transform.GetChild(0).GetChild((curShapeNum + 1) % 2).position = new Vector3(0, -10, 0);
                    replayTargetList[i] = experimentVars.replayGroup.transform.GetChild(0).GetChild(curShapeNum).gameObject;
                }

                // hands
                if (i == 2 || i == 3)
                {
                    Vector3 handPos = new Vector3(float.Parse(replayValues[start]), float.Parse(replayValues[start + 1]), float.Parse(replayValues[start + 2]));

                    // tracking lost on hands
                    if (handPos == gameVars.cameraLocation)
                    {
                        replayTargetList[i].SetActive(false);
                        continue;
                    } else
                    {
                        replayTargetList[i].SetActive(true);
                    }
                }
                // left eye
                if (i == 4)
                {
                    var hp = new Vector3(float.Parse(replayValues[10]), float.Parse(replayValues[11]), float.Parse(replayValues[12]));
                    var leftEyeVector = new Quaternion(float.Parse(replayValues[start + 3]), float.Parse(replayValues[start + 4]), float.Parse(replayValues[start + 5]), float.Parse(replayValues[start + 6])) * Vector3.forward;
                    RaycastHit leftEyeHit;
                    if (Physics.Raycast(hp, leftEyeVector, out leftEyeHit))
                    {
                        experimentVars.replayGroup.transform.GetChild(4).position = hp + new Vector3(-0.1f, 0, 0);

                        LineRenderer lr = experimentVars.replayGroup.transform.GetChild(4).GetComponent<LineRenderer>();
                        lr.SetPosition(0, hp);
                        lr.SetPosition(1, leftEyeHit.point);
                    }
                    continue;
                }

                // right eye
                if (i == 5)
                {
                    var hp = new Vector3(float.Parse(replayValues[10]), float.Parse(replayValues[11]), float.Parse(replayValues[12]));
                    var rightEyeVector = new Quaternion(float.Parse(replayValues[start + 3]), float.Parse(replayValues[start + 4]), float.Parse(replayValues[start + 5]), float.Parse(replayValues[start + 6])) * Vector3.forward;
                    RaycastHit rightEyeHit;
                    if (Physics.Raycast(hp, rightEyeVector, out rightEyeHit))
                    {
                        experimentVars.replayGroup.transform.GetChild(5).position = hp + new Vector3(0.1f, 0, 0);

                        LineRenderer lr = experimentVars.replayGroup.transform.GetChild(5).GetComponent<LineRenderer>();
                        lr.SetPosition(0, hp);
                        lr.SetPosition(1, rightEyeHit.point);
                    }
                    continue;
                }

                replayTargetList[i].transform.position = new Vector3(float.Parse(replayValues[start]), float.Parse(replayValues[start+1]), float.Parse(replayValues[start+2]));
                replayTargetList[i].transform.rotation = new Quaternion(float.Parse(replayValues[start + 3]), float.Parse(replayValues[start + 4]), float.Parse(replayValues[start + 5]), float.Parse(replayValues[start + 6]));
            }

            for (int i = 0; i < replayTargetBoneList.Count; i++)
            {
                int start = 7 * (i + replayTargetList.Count) + 3;

                replayTargetBoneList[i].Transform.position = new Vector3(float.Parse(replayValues[start]), float.Parse(replayValues[start + 1]), float.Parse(replayValues[start + 2]));
                replayTargetBoneList[i].Transform.rotation = new Quaternion(float.Parse(replayValues[start + 3]), float.Parse(replayValues[start + 4]), float.Parse(replayValues[start + 5]), float.Parse(replayValues[start + 6]));
            }

        } 

        if (curLine >= numLines) {
            EndReplay();
        }

        curLine++;
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
