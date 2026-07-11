using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class DroneModelCreator : EditorWindow
{
    static readonly Dictionary<Color, Material> _matCache = new Dictionary<Color, Material>();

    [MenuItem("Drone/Create Drone Model")]
    static void CreateDroneModel()
    {
        var selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select the 'Drone' object in the Hierarchy before running.", "OK");
            return;
        }

        _matCache.Clear();

        var existing = selected.transform.Find("Model");
        if (existing != null) DestroyImmediate(existing.gameObject);

        var model = new GameObject("Model");
        model.transform.SetParent(selected.transform, false);

        // ======== ボディ ========
        CreatePrim(PrimitiveType.Cylinder, model.transform, "Body",
            Vector3.zero, new Vector3(0.38f, 0.045f, 0.38f), new Color(0.12f, 0.12f, 0.14f));
        CreatePrim(PrimitiveType.Sphere, model.transform, "TopDome",
            new Vector3(0f, 0.055f, 0f), new Vector3(0.22f, 0.1f, 0.22f), new Color(0.08f, 0.08f, 0.1f));
        CreatePrim(PrimitiveType.Cube, model.transform, "Battery",
            new Vector3(0f, -0.055f, 0f), new Vector3(0.18f, 0.04f, 0.1f), new Color(0.2f, 0.2f, 0.22f));
        CreatePrim(PrimitiveType.Cube, model.transform, "Camera",
            new Vector3(0f, -0.01f, 0.21f), new Vector3(0.06f, 0.05f, 0.04f), new Color(0.05f, 0.05f, 0.05f));
        CreatePrim(PrimitiveType.Sphere, model.transform, "CameraLens",
            new Vector3(0f, -0.01f, 0.23f), new Vector3(0.025f, 0.025f, 0.025f), new Color(0.05f, 0.15f, 0.35f));
        CreatePrim(PrimitiveType.Sphere, model.transform, "LEDFront",
            new Vector3(0f, 0.0f, 0.2f), new Vector3(0.018f, 0.018f, 0.018f), new Color(1f, 0.1f, 0.1f));
        CreatePrim(PrimitiveType.Sphere, model.transform, "LEDRear",
            new Vector3(0f, 0.0f, -0.2f), new Vector3(0.018f, 0.018f, 0.018f), new Color(0.1f, 1f, 0.1f));

        // ======== アーム ========
        var armPositions = new Vector3[]
        {
            new Vector3(-0.5f, 0f,  0.5f),
            new Vector3( 0.5f, 0f,  0.5f),
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f, -0.5f),
        };
        string[] armNames = { "ArmFL", "ArmFR", "ArmBL", "ArmBR" };
        for (int i = 0; i < 4; i++)
            CreateArm(model.transform, armNames[i], armPositions[i]);

        // ======== モーター＋プロペラガード ========
        string[] motorNames = { "MotorFL", "MotorFR", "MotorBL", "MotorBR" };
        for (int i = 0; i < 4; i++)
        {
            var tip = armPositions[i];
            CreatePrim(PrimitiveType.Cylinder, model.transform, motorNames[i],
                new Vector3(tip.x, 0.045f, tip.z), new Vector3(0.085f, 0.038f, 0.085f), new Color(0.85f, 0.45f, 0.05f));
            CreatePrim(PrimitiveType.Cylinder, model.transform, motorNames[i] + "_Cap",
                new Vector3(tip.x, 0.09f, tip.z), new Vector3(0.05f, 0.02f, 0.05f), new Color(0.7f, 0.35f, 0.05f));
            for (int j = 0; j < 4; j++)
            {
                float angle = j * 90f * Mathf.Deg2Rad;
                float r = 0.15f;
                var pos = new Vector3(tip.x + Mathf.Sin(angle) * r, 0.07f, tip.z + Mathf.Cos(angle) * r);
                CreatePrim(PrimitiveType.Cylinder, model.transform, motorNames[i] + "_Guard" + j,
                    pos, new Vector3(0.012f, 0.022f, 0.012f), new Color(0.25f, 0.25f, 0.28f));
            }
        }

        // ======== ランディングギア ========
        var legOffsets = new Vector3[]
        {
            new Vector3(-0.15f, 0f,  0.22f),
            new Vector3( 0.15f, 0f,  0.22f),
            new Vector3(-0.15f, 0f, -0.22f),
            new Vector3( 0.15f, 0f, -0.22f),
        };
        for (int i = 0; i < 4; i++)
        {
            CreatePrim(PrimitiveType.Cylinder, model.transform, "Leg" + i,
                new Vector3(legOffsets[i].x, -0.1f, legOffsets[i].z),
                new Vector3(0.018f, 0.065f, 0.018f), new Color(0.2f, 0.2f, 0.22f));
            CreatePrim(PrimitiveType.Cube, model.transform, "Foot" + i,
                new Vector3(legOffsets[i].x, -0.16f, legOffsets[i].z),
                new Vector3(0.018f, 0.012f, 0.1f), new Color(0.18f, 0.18f, 0.2f));
        }

        // ======== プロペラブレード ========
        string[] propNames = { "PropFL", "PropFR", "PropBL", "PropBR" };
        bool[] propCW = { true, false, false, true };
        for (int i = 0; i < 4; i++)
        {
            var propTrans = selected.transform.Find(propNames[i]);
            if (propTrans == null) continue;
            var oldRenderer = propTrans.GetComponent<Renderer>();
            if (oldRenderer != null) oldRenderer.enabled = false;
            EnhancePropeller(propTrans, propCW[i]);
        }

        AssetDatabase.SaveAssets();
        Undo.RegisterCreatedObjectUndo(model, "Create Drone Model");
        Debug.Log("[DroneModelCreator] Done. Drone model generated.");
    }

    static GameObject CreatePrim(PrimitiveType type, Transform parent, string name,
        Vector3 pos, Vector3 scale, Color color)
    {
        var obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = pos;
        obj.transform.localScale = scale;
        SetColor(obj, color);
        return obj;
    }

    static void CreateArm(Transform parent, string name, Vector3 tipPos)
    {
        var arm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        arm.name = name;
        arm.transform.SetParent(parent, false);
        Vector3 center = tipPos * 0.5f;
        arm.transform.localPosition = new Vector3(center.x, 0f, center.z);
        float length = tipPos.magnitude;
        arm.transform.localScale = new Vector3(0.042f, length * 0.5f, 0.042f);
        float angle = Mathf.Atan2(tipPos.x, tipPos.z) * Mathf.Rad2Deg;
        arm.transform.localEulerAngles = new Vector3(90f, 0f, -angle);
        SetColor(arm, new Color(0.18f, 0.18f, 0.2f));

        var rib = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rib.name = name + "_Rib";
        rib.transform.SetParent(parent, false);
        rib.transform.localPosition = new Vector3(center.x, -0.01f, center.z);
        rib.transform.localScale = new Vector3(0.035f, 0.025f, length * 0.6f);
        rib.transform.localEulerAngles = new Vector3(0f, angle, 0f);
        SetColor(rib, new Color(0.15f, 0.15f, 0.17f));
    }

    static void EnhancePropeller(Transform parent, bool clockwise)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            DestroyImmediate(parent.GetChild(i).gameObject);

        var bladeColor = new Color(0.08f, 0.08f, 0.08f);
        var hubColor   = new Color(0.3f, 0.3f, 0.32f);

        CreatePrim(PrimitiveType.Cylinder, parent, "Hub",
            new Vector3(0f, 0.01f, 0f), new Vector3(0.04f, 0.015f, 0.04f), hubColor);

        float pitchAngle = clockwise ? -8f : 8f;
        for (int b = 0; b < 2; b++)
        {
            float angle = b * 180f;
            var blade = new GameObject("Blade" + b);
            blade.transform.SetParent(parent, false);
            blade.transform.localEulerAngles = new Vector3(0f, angle, 0f);

            var inner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            inner.name = "BladeInner";
            inner.transform.SetParent(blade.transform, false);
            inner.transform.localPosition    = new Vector3(0f, 0.04f, 0.13f);
            inner.transform.localScale       = new Vector3(0.18f, 0.04f, 0.24f);
            inner.transform.localEulerAngles = new Vector3(pitchAngle, 0f, 0f);
            SetColor(inner, bladeColor);

            var outer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            outer.name = "BladeOuter";
            outer.transform.SetParent(blade.transform, false);
            outer.transform.localPosition    = new Vector3(0f, 0.04f, 0.33f);
            outer.transform.localScale       = new Vector3(0.12f, 0.035f, 0.2f);
            outer.transform.localEulerAngles = new Vector3(pitchAngle * 1.5f, 0f, 0f);
            SetColor(outer, bladeColor);

            var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = "BladeTip";
            tip.transform.SetParent(blade.transform, false);
            tip.transform.localPosition = new Vector3(0f, 0.04f, 0.44f);
            tip.transform.localScale    = new Vector3(0.08f, 0.032f, 0.08f);
            SetColor(tip, bladeColor);
        }
    }

    static void SetColor(GameObject obj, Color color)
    {
        var mat = GetOrCreateMaterial(color);
        obj.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static Material GetOrCreateMaterial(Color color)
    {
        if (_matCache.TryGetValue(color, out var cached) && cached != null)
            return cached;

        const string dir = "Assets/Materials/Drone";
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/Materials", "Drone");

        var name    = $"DroneColor_{(int)(color.r * 255):X2}{(int)(color.g * 255):X2}{(int)(color.b * 255):X2}";
        var path    = $"{dir}/{name}.mat";
        var mat     = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard");
            mat = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.color = color;
            EditorUtility.SetDirty(mat);
        }

        _matCache[color] = mat;
        return mat;
    }
}
