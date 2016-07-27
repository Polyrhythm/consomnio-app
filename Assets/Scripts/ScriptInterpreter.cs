using UnityEngine;
using UnityEngine.UI;
using Miniscript;
using HoloToolkit.Unity;

public partial class ScriptInterpreter : Singleton<ScriptInterpreter> {
    public Interpreter interpreter;
    public InputField repl;
    public GameObject sceneCursor;
    public GameObject sceneContainer;

    private SceneInterpreter sceneInterpreter;
    private bool currentLineMarked = false;

    void Awake()
    {
        interpreter = new Interpreter();
        interpreter.standardOutput = (string s) => Output(s, "#0000ffff");
        interpreter.implicitOutput = (string s) => Output(s, "#00ff00ff");
        interpreter.errorOutput = (string s) =>
        {
            Output(s, "#ff0000ff");
            interpreter.Stop();
        };

        sceneInterpreter = SceneInterpreter.CreateInstance<SceneInterpreter>().initialize(interpreter, sceneCursor, sceneContainer);
        interpreter.REPL("", 0.1f);
        sceneInterpreter.setupScene();
    }

    void Update()
    {
        if (interpreter.Running())
        {
            interpreter.RunUntilDone(0.01f);
            return;
        }

        GetCommand();
    }

    // Sends a message to the REPL.
    void Output(string msg, string color)
    {
        repl.text += "<color=" + color + ">" + msg + "</color>\n";
    }

    // This is intended for handling complete chucnks of code being sent to the interpreter.
    public void RunScript(string source)
    {
        interpreter.Reset(source);
        interpreter.Compile();

        try
        {
            interpreter.RunUntilDone(0.01);
        } catch (MiniscriptException err)
        {
            Output(err.Description(), "#ff0000ff");
        }
    }

    // This is intended for REPL-like single commands being sent, and continuously watches
    // for new commands.
    public void HandleCommand(string command)
    {
        currentLineMarked = false;

        if (interpreter.Running() && !interpreter.NeedMoreInput())
        {
            return;
        }

        command = command.Trim();
        interpreter.REPL(command, 0.1f);

        GetCommand();
    }

    void GetCommand()
    {
        if (!interpreter.NeedMoreInput())
        {
            return;
        }

        if (!currentLineMarked)
        {
            repl.text += "~ ";
            currentLineMarked = true;
        }
    }
}
