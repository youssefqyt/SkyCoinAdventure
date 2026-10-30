using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SkyCoin.EditorTools
{
    /// <summary>
    /// EDITOR ONLY. Small building blocks used by <see cref="SkyCoinProjectBuilder"/> to
    /// construct scenes, prefabs and UI from code.
    ///
    /// Everything here is additive and idempotent: it creates objects and assets, it never
    /// deletes anything you made by hand.
    /// </summary>
    public static class SkyCoinFactory
    {
        public const string GameFolder = "Assets/_Game";
        public const string MaterialsFolder = "Assets/_Game/Materials";
        public const string PrefabsFolder = "Assets/_Game/Prefabs";
        public const string DataFolder = "Assets/_Game/Data";
        public const string ScenesFolder = "Assets/_Game/Scenes";
        public const string SettingsFolder = "Assets/_Game/Settings";

        /// <summary>The project's UI input actions. Used to give the EventSystem working pointer/click actions.</summary>
        public const string UiActionsAssetPath = "Assets/InputSystem_Actions.inputactions";

        // Colours used by the generated UI, kept in one place for consistency.
        public static readonly Color PanelBackdrop = new Color(0.05f, 0.07f, 0.11f, 0.82f);
        public static readonly Color PanelSolid = new Color(0.10f, 0.13f, 0.19f, 0.97f);
        public static readonly Color ButtonNormal = new Color(0.22f, 0.55f, 0.85f, 1f);
        public static readonly Color ButtonPrimary = new Color(0.20f, 0.68f, 0.45f, 1f);
        public static readonly Color ButtonDanger = new Color(0.72f, 0.28f, 0.28f, 1f);
        public static readonly Color ButtonNeutral = new Color(0.34f, 0.38f, 0.46f, 1f);

        // ------------------------------------------------------------------
        //  Folders and assets
        // ------------------------------------------------------------------

        public static void EnsureFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;

            string[] parts = assetFolderPath.Split('/');
            string current = parts[0];   // "Assets"

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        public static Material LoadMaterial(string materialName)
        {
            string path = $"{MaterialsFolder}/{materialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Debug.LogWarning($"[SkyCoin] Material '{materialName}' not found at {path}. " +
                                 "Run 'Sky Coin Adventure > Setup > 1. Create Materials' first.");
            }

            return material;
        }

        // ------------------------------------------------------------------
        //  3D objects
        // ------------------------------------------------------------------

        public static GameObject CreateEmpty(string name, Transform parent, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        public static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material material, int layer,
            Vector3 eulerAngles = default, bool keepCollider = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.transform.localEulerAngles = eulerAngles;
            go.layer = layer;

            if (material != null)
            {
                Renderer renderer = go.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = material;
            }

            if (!keepCollider)
            {
                Collider collider = go.GetComponent<Collider>();
                if (collider != null) Object.DestroyImmediate(collider);
            }

            return go;
        }

        public static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        /// <summary>Turns a layer name into its index, or -1 with a clear error.</summary>
        public static int RequireLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);

            if (layer < 0)
            {
                Debug.LogError($"[SkyCoin] The layer '{layerName}' does not exist. " +
                               "Add it in Edit > Project Settings > Tags and Layers, then run the build again.");
            }

            return layer;
        }

        // ------------------------------------------------------------------
        //  Lighting and post processing
        // ------------------------------------------------------------------

        public static GameObject CreateDirectionalLight(Transform parent)
        {
            GameObject go = new GameObject("Directional Light");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;

            return go;
        }

        public static GameObject CreateGlobalVolume(Transform parent)
        {
            EnsureFolder(SettingsFolder);

            string profilePath = $"{SettingsFolder}/SkyCoinVolumeProfile.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            // Add the overrides once. Add<T>() THROWS when the type is already present (and it
            // will be, because this profile asset is reused across builds and scenes), so we
            // look the component up first and only create it when it is missing.
            Tonemapping tonemapping = GetOrAddVolumeComponent<Tonemapping>(profile);
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.Neutral;

            Bloom bloom = GetOrAddVolumeComponent<Bloom>(profile);
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.25f;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.05f;

            Vignette vignette = GetOrAddVolumeComponent<Vignette>(profile);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.25f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.4f;

            EditorUtility.SetDirty(profile);

            GameObject go = new GameObject("Global Volume");
            go.transform.SetParent(parent, false);

            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;

            return go;
        }

        /// <summary>
        /// Returns the existing override of type <typeparamref name="T"/>, or adds it when the
        /// profile does not have it yet. <see cref="VolumeProfile.Add{T}"/> throws when the
        /// component is already present, which happens on every rebuild because the profile
        /// asset is shared.
        /// </summary>
        private static T GetOrAddVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            return profile.TryGet<T>(out T existing) ? existing : profile.Add<T>(true);
        }

        public static Camera CreateCamera(string name, Transform parent, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;

            Camera camera = go.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 400f;
            camera.clearFlags = CameraClearFlags.Skybox;

            go.AddComponent<AudioListener>();

            // URP needs its own camera data component. Adding the camera through the editor
            // does not always add it, so it is explicit here.
            if (go.GetComponent<UniversalAdditionalCameraData>() == null)
            {
                go.AddComponent<UniversalAdditionalCameraData>();
            }

            go.tag = "MainCamera";
            return camera;
        }

        // ------------------------------------------------------------------
        //  UI
        // ------------------------------------------------------------------

        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder = 0)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            return canvas;
        }

        public static GameObject CreateEventSystem(Transform parent)
        {
            GameObject go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);

            go.AddComponent<EventSystem>();

            InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();

            // Give the module real pointer / click / submit actions.
            //
            // We deliberately never call module.AssignDefaultActions(). On this Input System
            // version that makes the module re-derive its action references against the
            // in-memory DefaultInputActions and throws:
            //   InvalidOperationException: Action 'UI/Point[/Mouse/position]' must be part of an
            //   InputActionAsset in order to be able to create an InputActionReference for it.
            // (The module has no [ExecuteAlways], so its OnEnable does not run in edit mode and
            // cannot be relied on either.) Actions taken from a real .inputactions file always
            // have a backing asset, so this path is deterministic and also survives saving and
            // reloading the scene.
            InputActionAsset uiActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(UiActionsAssetPath);
            InputActionMap ui = uiActions != null ? uiActions.FindActionMap("UI") : null;

            if (ui == null)
            {
                Debug.LogWarning($"[SkyCoin] Could not read the 'UI' action map from '{UiActionsAssetPath}'. " +
                                 "The EventSystem was created, but menu buttons may not react to the mouse. " +
                                 "Create one via Assets > Create > Input Actions named InputSystem_Actions.");
                return go;
            }

            module.actionsAsset = uiActions;
            module.point = MakeActionReference(ui, "Point");
            module.move = MakeActionReference(ui, "Navigate");
            module.submit = MakeActionReference(ui, "Submit");
            module.cancel = MakeActionReference(ui, "Cancel");
            module.leftClick = MakeActionReference(ui, "Click");
            module.rightClick = MakeActionReference(ui, "RightClick");
            module.middleClick = MakeActionReference(ui, "MiddleClick");
            module.scrollWheel = MakeActionReference(ui, "ScrollWheel");

            return go;
        }

        /// <summary>Builds an <see cref="InputActionReference"/> for a named action, or null when it is absent.</summary>
        private static InputActionReference MakeActionReference(InputActionMap map, string actionName)
        {
            InputAction action = map.FindAction(actionName);
            return action != null ? InputActionReference.Create(action) : null;
        }

        /// <summary>A stretched UI element used for backdrops and panels.</summary>
        public static GameObject CreatePanel(string name, Transform parent, Color color, bool blocksClicks)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = blocksClicks;

            Stretch(image.rectTransform);
            return go;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// An invisible full screen container. Used to group a modal panel with its backdrop so
        /// that one SetActive() shows or hides the whole thing.
        /// </summary>
        public static RectTransform CreateUiGroup(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            Stretch(rect);
            return rect;
        }

        /// <summary>A centered card, used for the pause menu, options and result panels.</summary>
        public static RectTransform CreateCenterCard(string name, Transform parent, Vector2 size, Color color, bool blocksClicks = true)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = blocksClicks;

            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            return rect;
        }

        public static TMP_Text CreateText(string name, Transform parent, string text, float fontSize,
            TextAlignmentOptions alignment, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;                  // HUD text must never eat clicks

            return label;
        }

        /// <summary>Places a rect using a top-left anchor. y is how far DOWN from the top.</summary>
        public static RectTransform PlaceTopLeft(TMP_Text label, float x, float y, float width, float height)
        {
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
            return rect;
        }

        /// <summary>Places a rect using a top-right anchor. x is how far LEFT from the right edge.</summary>
        public static RectTransform PlaceTopRight(TMP_Text label, float x, float y, float width, float height)
        {
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(-x, -y);
            return rect;
        }

        /// <summary>Places a rect in the middle of its parent, offset by (x, y).</summary>
        public static void PlaceCentered(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
        }

        public static Button CreateButton(string name, Transform parent, string label, float x, float y,
            float width, float height, Color tint, float fontSize = 26f)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = true;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;

            PlaceCentered(image.rectTransform, x, y, width, height);

            // Tint is applied through the button's colour block so hover and pressed states
            // are clearly visible without extra artwork.
            ColorBlock colors = button.colors;
            colors.normalColor = tint;
            colors.highlightedColor = tint * 1.25f;
            colors.pressedColor = tint * 0.8f;
            colors.selectedColor = tint;
            colors.disabledColor = new Color(tint.r, tint.g, tint.b, 0.35f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            // Navigation OFF: arrow keys and Enter belong to the player, not to the menu.
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            TMP_Text text = CreateText("Label", go.transform, label, fontSize, TextAlignmentOptions.Center, Color.white);
            Stretch(text.rectTransform);

            return button;
        }

        public static Slider CreateSlider(string name, Transform parent, float x, float y, float width)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Slider slider = go.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            RectTransform rootRect = go.GetComponent<RectTransform>();
            PlaceCentered(rootRect, x, y, width, 32f);

            // Background
            GameObject background = new GameObject("Background", typeof(RectTransform));
            background.transform.SetParent(go.transform, false);
            Image backgroundImage = background.AddComponent<Image>();
            backgroundImage.color = new Color(0.08f, 0.10f, 0.14f, 1f);
            RectTransform backgroundRect = backgroundImage.rectTransform;
            backgroundRect.anchorMin = new Vector2(0f, 0.25f);
            backgroundRect.anchorMax = new Vector2(1f, 0.75f);
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            // Fill area
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5f, 0f);
            fillAreaRect.offsetMax = new Vector2(-15f, 0f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = ButtonNormal;
            RectTransform fillRect = fillImage.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.sizeDelta = new Vector2(10f, 0f);

            // Handle area
            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);

            GameObject handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(handleArea.transform, false);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.95f, 0.97f, 1f, 1f);
            RectTransform handleRect = handleImage.rectTransform;
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;
            handleRect.sizeDelta = new Vector2(22f, 0f);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;

            Navigation navigation = slider.navigation;
            navigation.mode = Navigation.Mode.None;
            slider.navigation = navigation;

            return slider;
        }

        // ------------------------------------------------------------------
        //  Serialized field access
        // ------------------------------------------------------------------

        /// <summary>
        /// Assigns a private [SerializeField] field on a component. Used instead of making
        /// the runtime scripts expose public setters that only the editor needs.
        /// </summary>
        public static bool SetReference(Object target, string fieldName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[SkyCoin] Field '{fieldName}' was not found on {target.GetType().Name}. " +
                               "The builder and the script have drifted apart.");
                return false;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        public static bool SetFloat(Object target, string fieldName, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[SkyCoin] Field '{fieldName}' was not found on {target.GetType().Name}.");
                return false;
            }

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        public static bool SetInt(Object target, string fieldName, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[SkyCoin] Field '{fieldName}' was not found on {target.GetType().Name}.");
                return false;
            }

            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        public static bool SetBool(Object target, string fieldName, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[SkyCoin] Field '{fieldName}' was not found on {target.GetType().Name}.");
                return false;
            }

            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        public static bool SetLayerMask(Object target, string fieldName, int layerMaskValue)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[SkyCoin] Field '{fieldName}' was not found on {target.GetType().Name}.");
                return false;
            }

            property.intValue = layerMaskValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        public static bool SetVector3(Object target, string fieldName, Vector3 value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[SkyCoin] Field '{fieldName}' was not found on {target.GetType().Name}.");
                return false;
            }

            property.vector3Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        public static bool SetVector2(Object target, string fieldName, Vector2 value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[SkyCoin] Field '{fieldName}' was not found on {target.GetType().Name}.");
                return false;
            }

            property.vector2Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        public static bool SetColor(Object target, string fieldName, Color value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[SkyCoin] Field '{fieldName}' was not found on {target.GetType().Name}.");
                return false;
            }

            property.colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>Returns every layer except the listed ones, as a LayerMask value.</summary>
        public static int EverythingExcept(params string[] layerNames)
        {
            int mask = ~0;
            List<string> names = new List<string>(layerNames);

            foreach (string name in names)
            {
                int layer = LayerMask.NameToLayer(name);
                if (layer >= 0) mask &= ~(1 << layer);
            }

            return mask;
        }
    }
}
