using UnityEngine;
using Miniscript;
using System.Collections.Generic;

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
        setupGetObjectPosition();

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
        buildMesh.code = (context, partialResult) =>
        {
            ValList verts = (ValList)context.GetVar("verts");
            ValList tris = (ValList)context.GetVar("tris");
            ValList uvs = (ValList)context.GetVar("uvs");
            ValList normals = (ValList)context.GetVar("normals");
            ValList tangetns = (ValList)context.GetVar("tangents");
            GameObject meshObj = new GameObject();
            meshObj.AddComponent<MeshFilter>();
            meshObj.AddComponent<MeshRenderer>();
            Mesh mesh = meshObj.GetComponent<MeshFilter>().sharedMesh;

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
            uniqueId += 1;
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