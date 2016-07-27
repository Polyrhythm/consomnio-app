using UnityEngine;
using UnityEngine.UI;
using HoloToolkit.Unity;

public partial class Workspace : Singleton<Workspace> {
    private InputField workspace;

    void Awake()
    {
        workspace = GetComponent<InputField>();
    }

    public void HandleSubmit()
    {
        ScriptInterpreter.Instance.RunScript(workspace.text.Trim());
        OnSelect();
    }

    public void OnSelect()
    {
        workspace.ActivateInputField();
    }

    public void OnDeselect()
    {
        workspace.DeactivateInputField();
    }
}
