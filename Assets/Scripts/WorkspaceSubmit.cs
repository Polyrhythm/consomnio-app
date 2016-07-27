using UnityEngine;

public class WorkspaceSubmit : MonoBehaviour {
    void OnSelect()
    {
        Workspace.Instance.HandleSubmit();
    }
}
