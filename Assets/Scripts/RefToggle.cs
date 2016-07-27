using UnityEngine;

public class RefToggle : MonoBehaviour {
    void OnSelect()
    {
        EditorController.Instance.toggleReference();
    }
}
