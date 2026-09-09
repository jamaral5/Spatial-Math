using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The title screen shown on launch. Built entirely at runtime on its own Canvas, above
/// everything else, and destroyed when the user presses Enter.
///
/// It does not need to disable the graph scripts to hold input back: the full-screen
/// backdrop is a raycast target, so PointerOverUI reports "over UI" everywhere, and the
/// orbit camera and surface picker already stand down when that is true.
///
/// The copy is deliberately kept in the fields below so it can be reworded in the
/// Inspector without touching code.
/// </summary>
[AddComponentMenu("Spatial Math/Start Screen")]
public class StartScreen : MonoBehaviour
{
    [Header("Masthead")]
    public string title = "Spatial Math";
    public string subtitle = "A learning solution by Jake Amaral";

    [Header("Body")]
    public string sectionOneHeading = "What it is";

    [TextArea(2, 5)]
    public string sectionOneBody =
        "A three-dimensional graphing calculator. Type any function of x and y and it " +
        "becomes a surface you can walk around, so the shape of an equation is something " +
        "you look at rather than imagine.";

    public string sectionTwoHeading = "What you can do";

    [TextArea(3, 8)]
    public string sectionTwoBody =
        "Plot any equation in real time — sin, cos, powers, roots and more\n" +
        "Orbit and zoom with the mouse to read the surface from any angle\n" +
        "Right-click the surface to render a tangent plane and read its equation\n" +
        "Fade the surface with the opacity slider to see the axes through it";

    [Header("Button")]
    public string buttonLabel = "Enter";

    [Header("Behaviour")]
    [Tooltip("Also dismiss the screen when the Return key is pressed.")]
    public bool acceptReturnKey = true;

    private GameObject screenRoot;
    private bool dismissed;

    void Start()
    {
        Build();
    }

    void Update()
    {
        if (dismissed || !acceptReturnKey) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            Dismiss();
    }

    /// <summary>Tears the title screen down. Hooked to the Enter button.</summary>
    public void Dismiss()
    {
        if (dismissed) return;
        dismissed = true;

        if (screenRoot != null) Destroy(screenRoot);
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

        // A centred column keeps the text off the screen edges at any aspect ratio.
        RectTransform column = UIKit.NewRect("Column", backdrop);
        column.anchorMin = new Vector2(0.5f, 0.5f);
        column.anchorMax = new Vector2(0.5f, 0.5f);
        column.pivot = new Vector2(0.5f, 0.5f);
        column.sizeDelta = new Vector2(620f, 620f);
        column.anchoredPosition = Vector2.zero;

        var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 6f;

        // ── Masthead ──────────────────────────────────────────────────
        AddText("Title", column, title, 68f, UIKit.Ink, TextAlignmentOptions.Center, 82f);
        AddRule(column, 2f, 14f);
        AddText("Subtitle", column, subtitle, 20f, UIKit.InkDim, TextAlignmentOptions.Center, 30f)
            .fontStyle = FontStyles.Italic;

        AddSpacer(column, 26f);

        // ── Body ──────────────────────────────────────────────────────
        AddText("HeadingOne", column, sectionOneHeading, 24f, UIKit.Neon, TextAlignmentOptions.Left, 32f);
        AddText("BodyOne", column, sectionOneBody, 17f, UIKit.Ink, TextAlignmentOptions.Left, 84f);

        AddSpacer(column, 14f);

        AddText("HeadingTwo", column, sectionTwoHeading, 24f, UIKit.Neon, TextAlignmentOptions.Left, 32f);
        AddText("BodyTwo", column, BulletList(sectionTwoBody), 17f, UIKit.Ink, TextAlignmentOptions.Left, 110f);

        AddSpacer(column, 26f);

        // ── Enter button ──────────────────────────────────────────────
        Button enter = UIKit.TextButton("EnterButton", column, buttonLabel, 22f, UIKit.Neon);
        var enterSize = enter.gameObject.AddComponent<LayoutElement>();
        enterSize.preferredHeight = 52f;
        enterSize.preferredWidth = 200f;
        enter.onClick.AddListener(Dismiss);
    }

    /// <summary>Turns one line per item into a bulleted block.</summary>
    private static string BulletList(string lines)
    {
        string[] parts = lines.Split('\n');
        for (int i = 0; i < parts.Length; i++)
        {
            string trimmed = parts[i].Trim();
            parts[i] = string.IsNullOrEmpty(trimmed) ? "" : "·  " + trimmed;
        }
        return string.Join("\n", parts);
    }

    private static TextMeshProUGUI AddText(string objectName, Transform parent, string text,
                                           float size, Color color, TextAlignmentOptions align,
                                           float height)
    {
        TextMeshProUGUI label = UIKit.Label(objectName, parent, text, size, color, align);
        var element = label.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        return label;
    }

    private static void AddRule(Transform parent, float thickness, float height)
    {
        RectTransform rule = UIKit.NewRect("Rule", parent);
        var image = rule.gameObject.AddComponent<Image>();
        image.color = new Color(UIKit.Neon.r, UIKit.Neon.g, UIKit.Neon.b, 0.45f);
        image.raycastTarget = false;

        var element = rule.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = thickness;
        element.minHeight = thickness;

        AddSpacer(parent, height - thickness);
    }

    private static void AddSpacer(Transform parent, float height)
    {
        RectTransform spacer = UIKit.NewRect("Spacer", parent);
        var element = spacer.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
    }
}
