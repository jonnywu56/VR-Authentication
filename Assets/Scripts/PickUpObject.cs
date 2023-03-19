using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PickUpObject : MonoBehaviour
{
    // Oculus inputs
    public GameObject leftHand;
    public GameObject rightHand;

    // Game variables
    public float throwForce = 1000f;
    public bool pickedUp = false;
    public Transform controller;

    // Oculus components
    private SkinnedMeshRenderer skr;
    private OVRHand leftOVRHand;
    private OVRHand rightOVRHand;

    // Tracked for throwing
    private List<Vector3> trackedPositions = new List<Vector3>();

    // Start is called before the first frame update
    void Start()
    {
        controller = null;
        skr = leftHand.GetComponent<SkinnedMeshRenderer>();
        leftOVRHand = leftHand.GetComponent<OVRHand>();
        rightOVRHand = rightHand.GetComponent<OVRHand>();
    }

    // Update is called once per frame
    void Update()
    {
        // Track most recent positions
        if (pickedUp)
        {
            if(trackedPositions.Count > 15)
            {
                trackedPositions.RemoveAt(0);
            }
            trackedPositions.Add(transform.position);
        }

        // Drop/throw item if needed
        if (pickedUp && controller.gameObject.tag == "LeftHand")
        {
            bool handsActive = skr.enabled && skr.sharedMesh != null;
            if ((!handsActive && !(OVRInput.Get(OVRInput.RawAxis1D.LIndexTrigger) > 0.9f)) || (handsActive && !leftOVRHand.GetFingerIsPinching(OVRHand.HandFinger.Index)))
            {
                Debug.Log("KILL");
                pickedUp = false;
                GameManager.Instance.SetGrab("left", false);
                Vector3 direction = trackedPositions[trackedPositions.Count - 1] - trackedPositions[0];
                Debug.Log(direction);
                GetComponent<Rigidbody>().AddForce(direction * throwForce);
                Destroy(GetComponent<FixedJoint>());
            }
        } else if (pickedUp && controller.gameObject.tag == "RightHand")
        {
            bool handsActive = skr.enabled && skr.sharedMesh != null;
            if ((!handsActive && !(OVRInput.Get(OVRInput.RawAxis1D.RIndexTrigger) > 0.9f)) || (handsActive && !rightOVRHand.GetFingerIsPinching(OVRHand.HandFinger.Index)))
            {
                pickedUp = false;
                GameManager.Instance.SetGrab("right", false);
                Vector3 direction = trackedPositions[trackedPositions.Count - 1] - trackedPositions[0];
                Debug.Log(direction);
                GetComponent<Rigidbody>().AddForce(direction * throwForce);
                Destroy(GetComponent<FixedJoint>());
            }
        }
    }

    // Hands/controller set to triggers
    private void OnTriggerStay(Collider col)
    {
        bool handsActive = skr.enabled && skr.sharedMesh != null;
        /**
        Debug.Log("STARt");
        Debug.Log(col.gameObject.tag);
        Debug.Log(!pickedUp);
        Debug.Log(handsActive);
        Debug.Log(!GameManager.Instance.CheckGrab("left"));
        Debug.Log(leftOVRHand.GetFingerIsPinching(OVRHand.HandFinger.Index));
        Debug.Log(!pickedUp && col.gameObject.tag == "LeftHand" && handsActive && !GameManager.Instance.CheckGrab("left") && leftOVRHand.GetFingerIsPinching(OVRHand.HandFinger.Index));
        **/

        // Pick up for left hand & left hand controller
        if ((!pickedUp && col.gameObject.tag == "LeftHand" && !handsActive && !GameManager.Instance.CheckGrab("left") && OVRInput.Get(OVRInput.RawAxis1D.LIndexTrigger) > 0.9f)
            || (!pickedUp && col.gameObject.tag == "LeftHand" && handsActive && !GameManager.Instance.CheckGrab("left") && IsPinching("left")))
        {
            pickedUp = true;
            controller = col.gameObject.transform;
            GameManager.Instance.SetGrab("left", true);
            trackedPositions.Clear();
            FixedJoint fj = this.gameObject.AddComponent<FixedJoint>() as FixedJoint;
            fj.connectedBody = col.gameObject.GetComponent<Rigidbody>();
        // Pickup for right hand & right hand controller
        } else if ((!pickedUp && col.gameObject.tag == "RightHand" && !handsActive && !GameManager.Instance.CheckGrab("right") && OVRInput.Get(OVRInput.RawAxis1D.RIndexTrigger) > 0.9f)
            || (!pickedUp && col.gameObject.tag == "RightHand" && handsActive && !GameManager.Instance.CheckGrab("right") && IsPinching("right")))
        {
            pickedUp = true;
            controller = col.gameObject.transform;
            GameManager.Instance.SetGrab("right", true);
            trackedPositions.Clear();
            FixedJoint fj = this.gameObject.AddComponent<FixedJoint>() as FixedJoint;
            fj.connectedBody = col.gameObject.GetComponent<Rigidbody>();
        }

    }

    // Boxes have base set for collision
    private void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.tag == "CubeBase")
        {
            if (this.gameObject.name.Contains("Cube"))
            {
                Destroy(this.gameObject);
                GameManager.Instance.IncreaseScore();
                GameManager.Instance.SpawnShape("");
            } else
            {
                Destroy(this.gameObject);
                GameManager.Instance.DecreaseScore();
                GameManager.Instance.SpawnShape("");
            }
        } else if (col.gameObject.tag == "SphereBase")
        {
            if (this.gameObject.name.Contains("Sphere"))
            {
                Destroy(this.gameObject);
                GameManager.Instance.IncreaseScore();
                GameManager.Instance.SpawnShape("");
            }
            else
            {
                Destroy(this.gameObject);
                GameManager.Instance.DecreaseScore();
                GameManager.Instance.SpawnShape("");
            }
        }
    }

    private List<OVRHand.HandFinger> fingers = new List<OVRHand.HandFinger> { OVRHand.HandFinger.Thumb, OVRHand.HandFinger.Index, OVRHand.HandFinger.Middle, OVRHand.HandFinger.Ring, OVRHand.HandFinger.Pinky };
    private bool IsPinching(string hand)
    {
        for (int i = 0; i < fingers.Count; i++)
        {
            // Readings for other fingers around ~0.2
            //Debug.Log(i.ToString() + " " + leftOVRHand.GetFingerPinchStrength(fingers[i]).ToString());
            if (hand == "left" && leftOVRHand.GetFingerPinchStrength(fingers[i]) > 0.75f) return true;
            if (hand == "right" && rightOVRHand.GetFingerPinchStrength(fingers[i]) > 0.75f) return true;
        }
        return false;
    }

}
