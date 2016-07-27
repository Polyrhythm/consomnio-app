using UnityEngine;

public class editorGazeCatcher : MonoBehaviour {
    void OnSelect()
    {
        if (EditorController.Instance.State == EditorController.EditorStates.REPL)
        {
            Console.Instance.OnSelect();
        }

        if (EditorController.Instance.State == EditorController.EditorStates.Workspace)
        {
            Workspace.Instance.OnSelect();
        }
    }
}
