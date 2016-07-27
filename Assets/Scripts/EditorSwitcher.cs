using UnityEngine;

public class EditorSwitcher : MonoBehaviour {
    void OnSelect()
    {
        EditorController.Instance.SwitchSpace();
    }
}
