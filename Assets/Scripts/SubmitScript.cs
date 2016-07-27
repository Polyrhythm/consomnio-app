using UnityEngine;
using UnityEngine.UI;

public class SubmitScript : MonoBehaviour {
    public InputField input;

    public void SubmitCode()
    {
        ScriptInterpreter.Instance.RunScript(input.text);
        input.text = "";
    }
}   
