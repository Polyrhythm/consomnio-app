using UnityEngine;
using UnityEngine.EventSystems;
using HoloToolkit.Unity;

public partial class EditorController : Singleton<EditorController> {
    public GameObject repl;
    public GameObject workspace;
    public GameObject reference;

    public enum EditorStates { REPL, Workspace }
    private EditorStates state;
    public EditorStates State
    {
        get
        {
            return state;
        }
        set
        {
            if (state == value) return;

            state = value;
            UpdateState();
        }
    }

    private bool refToggled = false;

    void Start()
    {
        State = EditorStates.REPL;
    }

    void UpdateState()
    {
        switch (state)
        {
            case EditorStates.REPL:
                workspace.SetActive(false);
                break;

            case EditorStates.Workspace:
                workspace.SetActive(true);
                break;

            default:
                break;
        }
    }

    public void SwitchSpace()
    {
        switch (state)
        {
            case EditorStates.REPL:
                State = EditorStates.Workspace;
                break;

            case EditorStates.Workspace:
                State = EditorStates.REPL;
                break;

            default:
                break;
        }
    }

    public void toggleReference()
    {
        switch(refToggled)
        {
            case true:
                reference.SetActive(false);
                break;

            case false:
                reference.SetActive(true);
                break;

            default:
                break;
        }

        refToggled = !refToggled;
    }
}
