using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ButtonLogic : MonoBehaviour
{
    public GameManager.GameMode buttonType;

    private void OnTriggerEnter(Collider col)
    {
        if (col.gameObject.tag == "LeftHand" || col.gameObject.tag == "RightHand")
        {
            if (buttonType == GameManager.GameMode.Game)
            {
                GameManager.Instance.EndLobby();
                GameManager.Instance.StartGame();
            } else if (buttonType == GameManager.GameMode.Replay)
            {
                GameManager.Instance.EndLobby();
                GameManager.Instance.StartReplay();
            }
        }

    }
}
    