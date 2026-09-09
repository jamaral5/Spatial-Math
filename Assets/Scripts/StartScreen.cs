using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The title screen shown on launch: masthead and an Enter button, nothing else.
///
/// It also acts as the gate for the rest of the interface. Every other panel starts
/// hidden and calls WhenDismissed to be shown, so the app opens on a clean title card
/// rather than on a screen full of controls.
///
/// The canvas is built in Awake, not Start, so that by the time any other component's
/// Start runs it already exists and UIKit.FindSceneCanvas can tell the two apart.
/// </summary>
[AddComponentMenu("Spatial Math/Start Screen")]
public class StartScreen : MonoBehaviour
{
    [Header("Masthead")]
    public string title = "Spatial Math";
    public string subtitle = "A learning solution by Jake Amaral";

    [Header("Button")]
    public string buttonLabel = "Enter";

    [Header("Behaviour")]
    [Tooltip("Also dismiss the screen when the Return key is pressed.")]
    public bool acceptReturnKey = true;

    // ─── Gate ──────────────────────────────────────────────────────────
    // Static so panels can ask about the title screen without holding a reference to it.

    /// <summary>The title screen's own canvas, so scene lookups can skip it.</summary>
    public static Canvas OverlayCanvas { get; private set; }

    /// <summary>True once a start screen has registered itself this session.</summary>
    public static bool Exists { get; private set; }

    /// <summary>True once the user has entered.</summary>
    public static bool Dismissed { get; private set; }

    private static System.Action pending;

    /// <summary>
    /// Runs the action when the user presses Enter — or immediately if there is no start
    /// screen in the scene, so removing this component never leaves the app unusable.
    /// </summary>
    public static void WhenDismissed(System.Action action)
    {
        if (action == null) return;

        if (!Exists || Dismissed)
        {
            action();
            return;
        }

        pending += action;
    }

    // ───────────────────────────────────────────────────────────────────

    private GameObject screenRoot;

    void Awake()
    {
        // Reset explicitly. With Enter Play Mode Options set to skip domain reload,
        // statics survive between play sessions and would otherwise start out stale.
        Exists = true;
        Dismissed = false;
        pending = null;
        OverlayCanvas = null;

        Build();
    }

    void Update()
    {
        if (Dismissed || !acceptReturnKey) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            Dismiss();
    }

    /// <summary>Tears the title screen down and releases everything waiting on it.</summary>
    public void Dismiss()
    {
        if (Dismissed) return;
        Dismissed = true;

        if (screenRoot != null) Destroy(screenRoot);
        OverlayCanvas = null;

        System.Action waiting = pending;
        pending = null;
        waiting?.Invoke();
    }

    // ───────────────────────────────────────────────────────────────────

    private void Build()
    {
        // Its own canvas, at a sorting order far above the gameplay UI, so nothing in
        // the scene can ever draw over the title screen.
        screenRoot = new GameObject("StartScreenCanvas");
        screenRoot.layer = LayerMask.NameToLayer("UI");

        var canvas = screenRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        OverlayCanvas = canvas;

        var scaler = screenRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        screenRoot.AddComponent<GraphicRaycaster>();

        // Backdrop. Opaque enough to hide the scene, and a raycast target so it swallows
        // every click that would otherwise reach the graph behind it.
        RectTransform backdrop = UIKit.Stretch(UIKit.NewRect("Backdrop", screenRoot.transform));
        var backdropImage = backdrop.gameObject.AddComponent<Image>();
        backdropImage.color = new Color(0.020f, 0.030f, 0.055f, 0.98f);
        backdropImage.raycastTarget = true;

        // A centred column keeps the masthead off the screen edges at any aspect ratio.
        RectTransform column = UIKit.NewRect("Column", backdrop);
        column.anchorMin = column.anchorMax = column.pivot = new Vector2(0.5f, 0.5f);
        column.sizeDelta = new Vector2(680f, 300f);
        column.anchoredPosition = Vector2.zero;

        var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 6f;

        AddText("Title", column, title, 76f, UIKit.Ink, 92f);
        AddRule(column);
        AddText("Subtitle", column, subtitle, 21f, UIKit.InkDim, 32f).fontStyle = FontStyles.Italic;

        AddSpacer(column, 34f);

        Button enter = UIKit.TextButton("EnterButton", column, buttonLabel, 22f, UIKit.Neon);
        var enterSize = enter.gameObject.AddComponent<LayoutElement>();
        enterSize.preferredHeight = 54f;
        enterSize.preferredWidth = 200f;
        enter.onClick.AddListener(Dismiss);
    }

    private static TextMeshProUGUI AddText(string objectName, Transform parent, string text,
                                           float size, Color color, float height)
    {
        TextMeshProUGUI label = UIKit.Label(objectName, parent, text, size, color,
                                            TextAlignmentOptions.Center);
        label.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return label;
    }

    private static void AddRule(Transform parent)
    {
        RectTransform rule = UIKit.NewRect("Rule", parent);
        var image = rule.gameObject.AddComponent<Image>();
        image.color = new Color(UIKit.Neon.r, UIKit.Neon.g, UIKit.Neon.b, 0.5f);
        image.raycastTarget = false;

        var element = rule.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 2f;
        element.minHeight = 2f;
    }

    private static void AddSpacer(Transform parent, float height)
    {
        RectTransform spacer = UIKit.NewRect("Spacer", parent);
        var element = spacer.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
    }
}
