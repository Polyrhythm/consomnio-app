using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class KeyHelper : MonoBehaviour {
    void OnSelect()
    {
        transform.parent.SendMessage("OnSelect", new BaseEventData(EventSystem.current));
    }

    void OnGazeEnter()
    {
        transform.parent.SendMessage("OnGazeEnter", new BaseEventData(EventSystem.current));
    }

    void OnGazeLeave()
    {
        transform.parent.SendMessage("OnGazeLeave", new BaseEventData(EventSystem.current));
    }
}
