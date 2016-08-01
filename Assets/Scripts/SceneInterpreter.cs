using UnityEngine;
using Miniscript;
using System.Collections.Generic;
using System.Collections;

public class SceneInterpreter : ScriptableObject
{
    private GameObject sceneCursor;
    private GameObject sceneContainer;
    private Interpreter interpreter;
    private Vector3 cursorStartPos;
    private Color currentColor = Color.white;
    private Vector3 currentRotation = new Vector3(0, 0, 0);
    private int uniqueId = 0;

    private Dictionary<int, GameObject> sceneObjects;

    public SceneInterpreter initialize(Interpreter interpreter, GameObject sceneCursor, GameObject sceneContainer)
    {
        this.interpreter = interpreter;
        this.sceneCursor = sceneCursor;
        this.sceneContainer = sceneContainer;

        // Place the scene cursor one meter in front of the camera to start.
        sceneCursor.transform.position = Camera.main.transform.position + new Vector3(0, 0, 1.5f);
        cursorStartPos = sceneContainer.transform.position;

        sceneObjects = new Dictionary<int, GameObject>();

        return this;
    }

    public void setupScene()
    {
        // Object creation.
        setupBuildPrimitive();
        setupBuildMesh();

        // Scene utils.
        setupClear();
        setupReset();
        setupRigidBody();
        setupDistance();
        setupGetObjectPosition();
        setupHelp();

        // Scene cursor manipulation.
        setupTranslate();
        setupColour();
        setupRotate();

        // Object manipulation.
        setupTranslateObject();
        setupColourObject();
        setupRotateObject();
        setupScaleObject();
        setupSetParent();
        setupUpdateVertices();
    }

    void setupHelp()
    {
        Intrinsic help = Intrinsic.Create("help");
        help.AddParam("topic", new ValString("default"));
        help.code = (context, partialResult) =>
        {
            ValMap docs = new ValMap();
            docs["general usage"] = new ValString("Consomnio is a live coding environment.\n" +
                "You have access to an in-scene 'cursor' which you can translate around and build objects with." +
                "Any objects you build (aside from procedural meshes) will be positioned at the cursor's position." +
                "You also have access to a REPL (open by default) and a workspace. Use the REPL for immediate execution of " +
                "single line commands and getting printed returns. Use the workspace for multi-line programs. Use the 'switch' button " +
                "to switch between REPL and workspace. The workspace has a 'submit' button for running your code.\n\n" +
                "Example workspace program that generates a cube of random colour in front of you\n" +
                "clear()\n" +
                "reset()\n" +
                "translate({\"x\": 0, \"y\": 0, \"z\": 1.5})\n\n" +
                "colour(rnd, rnd, rnd)\n" +
                "myCube = buildPrimitive(\"cube\", {\"x\": 0.05, \"y\": 0.05, \"z\": 0.05})");

            docs["UI"] = new ValString("Air tap the '?' button for a language quick reference.\n" +
                "Air tap the 'Switch' button to toggle between REPL and workspace modes.\n" +
                "Air tap the 'Submit' button to submit workspace code (if in the workspace).\n" +
                "Ctrl-c will copy text. Ctrl-v will paste text. Holding shift and using cursor keys will highlight text.");

            docs["Animation"] = new ValString("If you want things to move, I suggest using a while loop.\n" +
                "while 1 == 1\n" +
                "  translateObject(myCube, {\"x\": 0, \"y\": sin(time) * 0.001, \"z\": 0})\n" +
                "  wait(1/60) // <-- you need to insert a wait or the while loop will execute more quickly than frames are being rendered.\n" +
                "end while");

            docs["rigidBody"] = new ValString("number -> bool\n" +
                "Takes an object id (optional) and adds a ridibody to the object.\n" +
                "If no object is specificed, it adds rigidbodies to every object in the scene.\n" +
                "Returns a boolean indicating whether the function succeeded.\n" +
                "rigidBody(myCube)");

            docs["Language notes"] = new ValString("The sciprting language in Consomnio is a simple, object-oriented affair.\n" +
                "It is whitespace agnostic. You are free to add comments wherever you want with '//'." +
                "Behind the scenes, all the code you submit will hit pre-defined functions in C# which will deal directly with Unity.\n" +
                "Unity handles all the rendering.");

            docs["buildPrimitive"] = new ValString("string -> map -> string -> number\n" +
                "Builds a primitive of the specified shape, dimensions as a vec3, and material (optional).\n" +
                "Valid shapes are cube, sphere, plane, quad, capsule, and cylinder.\n" +
                "Dimensions is a map representing a vec3.\n" +
                "Material is a string and is optional. Valid materials are lambertTransparent and wireframe. Default is Unity default shader.\n" +
                "Returns the id of the shape so you can store a reference to it.\n" +
                "buildPrimitive(\"cube\", {\"x\": 0.05, \"y\": 0.05, \"z\": 0.05}, \"lambertTransparent\")");

            docs["translate"] = new ValString("map -> map\n" +
                "Translate the scene cursor to the specified vector. The translation is additive.\n" +
                "Returns a map with the cursor's new position as a vec3." +
                "translate({\"x\": 1, \"y\": 0.5, \"z\": 0}");

            docs["colour"] = new ValString("number -> number -> number -> number -> map\n" +
                "Takes RGBA values from 0 to 1 (a is optional) and sets and objects built in the future to this colour.\n" +
                "colour(0.5, 0.2, 1)");

            docs["reset"] = new ValString("Resets the scene cursor to origin, which is where your head was when you started the app.");

            docs["clear"] = new ValString("Clears all objects from the scene.");

            docs["rotate"] = new ValString("map -> map\n" +
                "Takes a vec3 map and rotates the cursor by euler angles.\n" +
                "Returns the euler angle representation of the new rotation." +
                "rotate({\"x\": 45, \"y\": 0, \"z\": 0}");

            docs["translateObject"] = new ValString("number -> map -> map\n" +
                "Takes an object id and a vec3 map.\n" +
                "Translates the object belonging to the given id by the given vec3.\n" +
                "Returns a vec3 of the object's current position.\n" +
                "translateObject(myCube, {\"x\": 1, \"y\": 0.5, \"z\": 0})");

            docs["colourObject"] = new ValString("number -> number -> number -> number -> number -> map\n" +
                "Takes an object id and R, G, B, A (optional) values as numbers between 0 and 1.\n" +
                "Changes the object's colour.\n" +
                "Returns a map representing the object's colour.\n" +
                "colourObject(myCube, 0.5, 0.2, 1)");

            docs["scaleObject"] = new ValString("number -> map -> map\n" +
                "Takes an object id and a vec3 map. Scales the object given.\n" +
                "Returns a vec3 of the scale of the object.\n" +
                "scaleObject(myCube, {\"x\": 1.2, \"y\": 1.2, \"z\": 1.2})");

            docs["rotateObject"] = new ValString("number -> map -> map\n" +
                "Takes an object id and a vec3 map representing euler angles. Rotates the given object.\n" +
                "returns a vec3 of euler angles of the object's rotation.\n" +
                "rotateObject(myCube, {\"x\": 0, \"y\": 90, \"z\": 45})");

            docs["buildMesh"] = new ValString("list -> list -> list -> list -> list -> string -> number\n" +
                "Takes lists for vertex vec3s, triangle vert indices, uv vec2s, normal vec3s (optional), tangent vec4s (optional), and material\n" +
                "Possible material value other than default is \"uvTest\"\n" +
                "Builds a procedural mesh from the given lists.\n" +
                "Returns an id representing the new object containing the mesh.\n" +
                "verts = range(0, 3)\n" +
                "verts[0] = {\"x\": 0, \"y\": 0, \"z\": 1.5}\n" +
                "... // continuing adding vertices, a tri list, and a uv list\n" +
                "myObj = buildMesh(verts, tris, uvs)");

            docs["setParent"] = new ValString("number -> number -> number\n" +
                "Takes a object id and parent object id. Sets the object to be a child of the parent.\n" +
                "Returns the id of the parent object.\n" +
                "setParent(myCube, myCubeHolder)");

            string[] topics = new string[docs.Count];
            for (int i = 0; i < docs.Count; i++)
            {
                ValMap thing = docs.GetKeyValuePair(i);
                topics[i] = thing["key"].ToString();
            }
            docs["default"] = new ValString(string.Join("\n", topics));
            string requestedDoc = context.GetVar("topic").ToString();
            
            return new Intrinsic.Result(new ValString(docs[requestedDoc].ToString()));
        };
    }

    void setupUpdateVertices()
    {
        Intrinsic updateVertices = Intrinsic.Create("updateVertices");
        updateVertices.AddParam("id");
        updateVertices.AddParam("verts");
        updateVertices.code = (context, partialResult) =>
        {
            int id = context.GetVar("id").IntValue();
            GameObject meshObj = sceneObjects[id];
            Mesh mesh = meshObj.GetComponent<MeshFilter>().mesh;
            ValList verts = (ValList)context.GetVar("verts");

            // Create vertices list.
            Value[] vertArray = verts.values.ToArray();
            Vector3[] vertList = new Vector3[vertArray.Length];
            for (int i = 0; i < vertArray.Length; i++)
            {
                ValMap vert = (ValMap)vertArray[i];
                vertList[i] = new Vector3(vert["x"].FloatValue(), vert["y"].FloatValue(), vert["z"].FloatValue());
            }
            mesh.vertices = vertList;

            return new Intrinsic.Result(new ValNumber(id));
        };
    }

    void setupBuildMesh()
    {
        Intrinsic buildMesh = Intrinsic.Create("buildMesh");
        // List of vec3.
        buildMesh.AddParam("verts", new ValList());
        // List of int.
        buildMesh.AddParam("tris", new ValList());
        // List of vec2.
        buildMesh.AddParam("uvs", new ValList());
        // List of vec3.
        buildMesh.AddParam("normals");
        // List of vec4.
        buildMesh.AddParam("tangents");
        buildMesh.AddParam("material", new ValString(""));
        buildMesh.code = (context, partialResult) =>
        {
            ValList verts = (ValList)context.GetVar("verts");
            ValList tris = (ValList)context.GetVar("tris");
            ValList uvs = (ValList)context.GetVar("uvs");
            ValList normals = (ValList)context.GetVar("normals");
            ValList tangents = (ValList)context.GetVar("tangents");
            GameObject meshObj = new GameObject();
            meshObj.AddComponent<MeshFilter>();
            meshObj.AddComponent<MeshRenderer>();
            Mesh mesh = meshObj.GetComponent<MeshFilter>().mesh;
            MeshRenderer renderer = meshObj.GetComponent<MeshRenderer>();

            string mat = context.GetVar("material").ToString();
            Material meshMat;
            switch (mat)
            {
                case "testUV":
                    meshMat = Materials.Instance.testUV;
                    break;
                default:
                    meshMat = Materials.Instance.standardMaterial;
                    break;
            }

            // Create vertices list.
            Value[] vertArray = verts.values.ToArray();
            Vector3[] vertList = new Vector3[vertArray.Length];
            for (int i = 0; i < vertArray.Length; i++)
            {
                ValMap vert = (ValMap)vertArray[i];
                vertList[i] = new Vector3(vert["x"].FloatValue(), vert["y"].FloatValue(), vert["z"].FloatValue());
            }
            mesh.vertices = vertList;

            // Create triangles list.
            Value[] triArray = tris.values.ToArray();
            int[] triList = new int[triArray.Length];
            for (int i = 0; i < triArray.Length; i++)
            {
                triList[i] = triArray[i].IntValue();
            }
            mesh.triangles = triList;

            // Create UV list.
            Value[] uvArray = uvs.values.ToArray();
            Vector2[] uvList = new Vector2[uvArray.Length];
            for (int i = 0; i < uvArray.Length; i++)
            {
                ValMap vec = (ValMap)uvArray[i];
                uvList[i] = new Vector2(vec["u"].FloatValue(), vec["v"].FloatValue());
            }
            mesh.uv = uvList;

            // Create normals if provided.
            Vector3[] normalsList;
            if (normals != null)
            {
                Value[] normalsArray = normals.values.ToArray();
                normalsList = new Vector3[vertList.Length];
                for (int i = 0; i < normalsArray.Length; i++)
                {
                    ValMap normal = (ValMap)normalsArray[i];
                    normalsList[i] = new Vector3(normal["x"].FloatValue(), normal["y"].FloatValue(), normal["z"].FloatValue()).normalized;
                }

                mesh.normals = normalsList;
            }

            Vector4[] tangentsList;
            if (tangents != null)
            {
                Value[] tangentsArray = tangents.values.ToArray();
                tangentsList = new Vector4[vertList.Length];
                for (int i = 0; i < tangentsArray.Length; i++)
                {
                    ValMap vec = (ValMap)tangentsArray[i];
                    tangentsList[i] = new Vector4(vec["x"].FloatValue(), vec["y"].FloatValue(), vec["z"].FloatValue(), vec["w"].FloatValue());
                }

                mesh.tangents = tangentsList;
            }

            // Assign a material;
            renderer.material = meshMat;

            uniqueId = uniqueId + 1;
            sceneObjects.Add(uniqueId, meshObj);

            return new Intrinsic.Result(new ValNumber(uniqueId));
        };
    }

    void setupDistance()
    {
        Intrinsic distance = Intrinsic.Create("distance");
        distance.AddParam("v1");
        distance.AddParam("v2");
        distance.code = (context, partialResult) =>
        {
            ValMap v1 = (ValMap)context.GetVar("v1");
            ValMap v2 = (ValMap)context.GetVar("v2");

            float resultDistance = Vector3.Distance(
                new Vector3(v1["x"].FloatValue(), v1["y"].FloatValue(), v1["z"].FloatValue()),
                new Vector3(v2["x"].FloatValue(), v2["y"].FloatValue(), v2["z"].FloatValue())
            );

            return new Intrinsic.Result(new ValNumber(resultDistance));
        };
    }

    void setupGetObjectPosition()
    {
        Intrinsic getObjectPosition = Intrinsic.Create("getObjectPosition");
        getObjectPosition.AddParam("objId");
        getObjectPosition.code = (context, partialResult) =>
        {
            int id = context.GetVar("objId").IntValue();

            Vector3 pos = sceneObjects[id].transform.position;
            ValMap result = new ValMap();
            result["x"] = new ValNumber(pos.x);
            result["y"] = new ValNumber(pos.y);
            result["z"] = new ValNumber(pos.z);

            return new Intrinsic.Result(result);
        };
    }

    void setupSetParent()
    {
        Intrinsic setParent = Intrinsic.Create("setParent");
        setParent.AddParam("objId");
        setParent.AddParam("parentId");
        setParent.code = (context, partialResult) =>
        {
            int objId = context.GetVar("objId").IntValue();
            int parentId = context.GetVar("parentId").IntValue();

            sceneObjects[objId].transform.SetParent(sceneObjects[parentId].transform);

            return new Intrinsic.Result(new ValNumber(parentId));
        };
    }

    void setupScaleObject()
    {
        Intrinsic scaleObject = Intrinsic.Create("scaleObject");
        scaleObject.AddParam("id", ValNumber.zero);
        scaleObject.AddParam("vec", new ValMap());
        scaleObject.code = (context, partialResult) =>
        {
            int id = context.GetVar("id").IntValue();

            if (sceneObjects[id] == null)
            {
                return new Intrinsic.Result(ValNumber.Truth(false));
            }

            GameObject obj = sceneObjects[id];
            ValMap vec = (ValMap)context.GetVar("vec");
            obj.transform.localScale = new Vector3(
                vec["x"].FloatValue(),
                vec["y"].FloatValue(),
                vec["z"].FloatValue()
            );

            return new Intrinsic.Result(new ValString(obj.transform.localScale.ToString()));
        };
    }

    void setupRigidBody()
    {
        Intrinsic rigidBody = Intrinsic.Create("rigidBody");
        rigidBody.AddParam("id", new ValString("all"));
        rigidBody.code = (context, partialResult) =>
        {
            if (context.GetVar("id").ToString() == "all") {
                foreach (KeyValuePair<int, GameObject> objPair in sceneObjects)
                {
                    Vector3 objScale = objPair.Value.transform.localScale;
                    objPair.Value.AddComponent<Rigidbody>().useGravity = false;
                    objPair.Value.GetComponent<Rigidbody>().mass = (objScale.x + objScale.y + objScale.z) / 3f;
                }
            }
            else if (sceneObjects[context.GetVar("id").IntValue()] == null)
            {
                return new Intrinsic.Result(ValNumber.Truth(false));
            } 
            else
            {
                GameObject obj = sceneObjects[context.GetVar("id").IntValue()];
                Vector3 objScale = obj.transform.localScale;
                obj.AddComponent<Rigidbody>().useGravity = false;
                obj.GetComponent<Rigidbody>().mass = (objScale.x + objScale.y + objScale.z) / 3f;
            }

            return new Intrinsic.Result(ValNumber.Truth(true));
        };
    }

    void setupRotateObject()
    {
        Intrinsic rotateObject = Intrinsic.Create("rotateObject");
        rotateObject.AddParam("id");
        rotateObject.AddParam("vec", new ValMap());
        rotateObject.code = (context, partialResult) =>
        {
            int id = context.GetVar("id").IntValue();

            if (sceneObjects[id] == null)
            {
                return new Intrinsic.Result(ValNumber.Truth(false));
            }

            GameObject obj = sceneObjects[id];

            ValMap vec = (ValMap)context.GetVar("vec");
            currentRotation = new Vector3(
                vec["x"].FloatValue(),
                vec["y"].FloatValue(),
                vec["z"].FloatValue()
            );

            obj.transform.Rotate(currentRotation, Space.Self);

            return new Intrinsic.Result(new ValString(currentRotation.ToString()));
        };
    }

    void setupColourObject()
    {
        Intrinsic colourObject = Intrinsic.Create("colourObject");
        colourObject.AddParam("id");
        colourObject.AddParam("r", ValNumber.one);
        colourObject.AddParam("g", ValNumber.one);
        colourObject.AddParam("b", ValNumber.one);
        colourObject.AddParam("a", ValNumber.one);
        colourObject.code = (context, partialResult) =>
        {
            int id = context.GetVar("id").IntValue();

            if (sceneObjects[id] == null)
            {
                return new Intrinsic.Result(ValNumber.Truth(false));
            }

            GameObject obj = sceneObjects[id];
            obj.GetComponent<MeshRenderer>().material.color = new Color(
                context.GetVar("r").FloatValue(),
                context.GetVar("g").FloatValue(),
                context.GetVar("b").FloatValue(),
                context.GetVar("a").FloatValue()
            );

            return new Intrinsic.Result(new ValString(obj.GetComponent<MeshRenderer>().material.color.ToString()));
        };
    }

    void setupTranslateObject()
    {
        Intrinsic translateObject = Intrinsic.Create("translateObject");
        translateObject.AddParam("id");
        translateObject.AddParam("vec", new ValMap());
        translateObject.code = (context, partialResult) =>
        {
            int id = context.GetVar("id").IntValue();

            if (sceneObjects[id] == null)
            {
                return new Intrinsic.Result(ValNumber.Truth(false));
            }

            ValMap vec = (ValMap)context.GetVar("vec");
            GameObject obj = sceneObjects[id];
            obj.transform.Translate(new Vector3(
                vec["x"].FloatValue(),
                vec["y"].FloatValue(),
                vec["z"].FloatValue()
            ));

            return new Intrinsic.Result(new ValString(obj.transform.position.ToString()));
        };
    }

    void setupTranslate()
    {
        Intrinsic translate = Intrinsic.Create("translate");
        translate.AddParam("vec", new ValMap());
        translate.code = (context, partialResult) =>
        {
            ValMap vec = (ValMap)context.GetVar("vec");
            Vector3 translateVector = new Vector3(
                vec["x"].FloatValue(),
                vec["y"].FloatValue(),
                vec["z"].FloatValue()
            );

            sceneCursor.transform.Translate(translateVector, Space.Self);
            ValMap result = new ValMap();
            result["x"] = new ValNumber(sceneCursor.transform.position.x);
            result["y"] = new ValNumber(sceneCursor.transform.position.y);
            result["z"] = new ValNumber(sceneCursor.transform.position.z);

            return new Intrinsic.Result(result);
        };
    }

    void setupBuildPrimitive()
    {
        Intrinsic buildPrimitive = Intrinsic.Create("buildPrimitive");
        buildPrimitive.AddParam("primitive", new ValString("cube"));
        buildPrimitive.AddParam("vec", new ValMap());
        buildPrimitive.AddParam("material", new ValString("standard"));
        buildPrimitive.code = (context, partialResult) =>
        {
            PrimitiveType primType;
            switch (context.GetVar("primitive").ToString())
            {
                case "cube":
                    primType = PrimitiveType.Cube;
                    break;
                case "sphere":
                    primType = PrimitiveType.Sphere;
                    break;
                case "quad":
                    primType = PrimitiveType.Quad;
                    break;
                case "plane":
                    primType = PrimitiveType.Plane;
                    break;
                case "capsule":
                    primType = PrimitiveType.Capsule;
                    break;

                default:
                    primType = PrimitiveType.Cube;
                    break;
            }

            Material mat;
            switch (context.GetVar("material").ToString())
            {
                case "lambertTransparent":
                    mat = Materials.Instance.lambertTransparent;
                    break;

                case "wireframe":
                    mat = Materials.Instance.wireframe;
                    break;
                
                default:
                    mat = Materials.Instance.standardMaterial;
                    break;
            }

            ValMap vec = (ValMap)context.GetVar("vec");
            GameObject primitive = GameObject.CreatePrimitive(primType);
            primitive.transform.localScale = new Vector3(
                vec["x"].FloatValue(),
                vec["y"].FloatValue(),
                vec["z"].FloatValue()
            );
            primitive.transform.position = sceneCursor.transform.position;
            primitive.transform.rotation = sceneCursor.transform.rotation;

            MeshRenderer renderer = primitive.GetComponent<MeshRenderer>();
            renderer.material = mat;
            renderer.material.color = currentColor;

            primitive.transform.SetParent(sceneContainer.transform);

            // Add new primitive to sceneObjects.
            uniqueId = uniqueId + 1;
            sceneObjects.Add(uniqueId, primitive);

            return new Intrinsic.Result(new ValNumber(uniqueId));
        };
    }

    void setupClear()
    {
        Intrinsic clear = Intrinsic.Create("clear");
        clear.code = (context, partialResult) =>
        {
            for (int i = sceneContainer.transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = sceneContainer.transform.GetChild(i).gameObject;
                Destroy(child);
            }

            sceneObjects.Clear();
            uniqueId = 0;

            return new Intrinsic.Result(ValNumber.Truth(true));
        };
    }

    void setupReset()
    {
        Intrinsic reset = Intrinsic.Create("reset");
        reset.code = (context, partialResult) =>
        {
            sceneCursor.transform.position = cursorStartPos;
            sceneCursor.transform.rotation = Quaternion.identity;

            ValMap result = new ValMap();
            result["x"] = new ValNumber(sceneCursor.transform.position.x);
            result["y"] = new ValNumber(sceneCursor.transform.position.y);
            result["z"] = new ValNumber(sceneCursor.transform.position.z);
            return new Intrinsic.Result(result);
        };
    }

    void setupColour()
    {
        Intrinsic colour = Intrinsic.Create("colour");
        colour.AddParam("r", ValNumber.one);
        colour.AddParam("g", ValNumber.one);
        colour.AddParam("b", ValNumber.one);
        colour.AddParam("a", ValNumber.one);
        colour.code = (context, partialResult) =>
        {
            currentColor = new Color(
                context.GetVar("r").FloatValue(),
                context.GetVar("g").FloatValue(),
                context.GetVar("b").FloatValue(),
                context.GetVar("a").FloatValue()
            );

            return new Intrinsic.Result(new ValString(currentColor.ToString()));
        };
    }

    void setupRotate()
    {
        Intrinsic rotate = Intrinsic.Create("rotate");
        rotate.AddParam("vec", new ValMap());
        rotate.code = (context, partialResult) =>
        {
            ValMap vec = (ValMap)context.GetVar("vec");
            currentRotation = new Vector3(
                vec["x"].FloatValue(),
                vec["y"].FloatValue(),
                vec["z"].FloatValue()
            );

            sceneCursor.transform.Rotate(currentRotation, Space.Self);

            return new Intrinsic.Result(new ValString(currentRotation.ToString()));
        };
    }
}