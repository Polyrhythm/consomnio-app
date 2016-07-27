using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using HoloToolkit.Unity;

public partial class Console : Singleton<Console> {
    private InputField console;


    void Awake()
    {
        console = GetComponent<InputField>();
    }

    void Start()
    {
        console.onEndEdit.AddListener(val =>
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) {
                HandleSubmit();
            }
        });
    }

    public void OnSelect()
    {
        console.ActivateInputField();
    }

    public void OnDeselect()
    {
        console.DeactivateInputField();
    }

    void HandleSubmit()
    {
        string lastLine = GetLastLine();
        NewLine();
        ScriptInterpreter.Instance.HandleCommand(lastLine);
    }

    IEnumerator MoveTextEnd_NextFrame()
    {
        yield return 0;
        console.MoveTextEnd(false);
    }

    string GetLastLine()
    {
        string[] splitText = console.text.Split('\n');
        splitText = splitText[splitText.Length - 1].Split('~');

        return splitText[splitText.Length - 1];
    }

    public void NewLine()
    {
        console.text += "\n";
        console.ActivateInputField();

        // Clear the selection of text that Unity does and move caret to end.
        StartCoroutine(MoveTextEnd_NextFrame());
    }
}

