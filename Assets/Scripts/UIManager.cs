using UnityEngine;
using TMPro;

/// <summary>
/// Connects the on-screen UI to the GraphManager.
///
/// It does two jobs:
///   1. The three preset buttons (sin·cos, paraboloid, saddle) — unchanged behavior,
///      but they now also fill the input box so the user can see/edit that equation.
///   2. A text box where the user can type ANY equation and plot it. All the heavy
///      lifting (parsing the text, building the mesh) already lives in EquationParser
///      and GraphManager — this script just passes the typed string along and reports
///      back whether it worked.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("References")]
    public GraphManager graphManager;

    [Header("Custom Equation Input")]
    [Tooltip("The text box the user types an equation into.")]
    public TMP_InputField equationInput;

    [Tooltip("Optional: a text label that shows 'Plotted!' or an error message.")]
    public TMP_Text statusText;

    [Tooltip("Which of the 3 graph slots typed equations go into.")]
    public int targetSlot = 0;

    void Start()
    {
        // Wire the input box to our handler in CODE, so you don't have to set up the
        // event in the Inspector. onSubmit fires when the user presses Enter while the
        // box is focused. The handler receives whatever text was in the box.
        if (equationInput != null)
            equationInput.onSubmit.AddListener(OnEquationSubmitted);
    }

    // ─── Preset buttons ────────────────────────────────────────────────
    // Each one now routes through ApplyEquation so the input box and status
    // text stay in sync with what's actually on screen.

    public void ShowSinCos()    => ApplyEquation("sin(x) * cos(y)");
    public void ShowParaboloid() => ApplyEquation("x^2 + y^2");
    public void ShowSaddle()    => ApplyEquation("x^2 - y^2");

    // ─── Custom input ──────────────────────────────────────────────────

    /// <summary>
    /// Called automatically when the user presses Enter in the input box.
    /// </summary>
    private void OnEquationSubmitted(string text)
    {
        ApplyEquation(text);
    }

    /// <summary>
    /// Hook this up to an optional "Plot" button's OnClick if you want a button
    /// instead of (or as well as) the Enter key. It reads the current box text.
    /// </summary>
    public void SubmitCurrentEquation()
    {
        if (equationInput != null)
            ApplyEquation(equationInput.text);
    }

    /// <summary>
    /// The single place where an equation actually gets sent to the graph, with
    /// success/failure feedback. Everything above funnels through here.
    /// </summary>
    private void ApplyEquation(string equation)
    {
        if (graphManager == null) return;

        // Keep the input box showing the current equation (handy for the preset buttons).
        if (equationInput != null && equationInput.text != equation)
            equationInput.SetTextWithoutNotify(equation);

        // SetEquation returns true if EquationParser understood the text. A typo like
        // "sin(x" (missing parenthesis) returns false instead of crashing.
        bool ok = graphManager.SetEquation(targetSlot, equation);

        if (statusText != null)
        {
            if (ok)
            {
                statusText.text = $"Plotted:  {equation}";
                statusText.color = new Color(0.4f, 1f, 0.5f);   // green
            }
            else
            {
                statusText.text = "Couldn't read that equation. Check for typos or unmatched ( ).";
                statusText.color = new Color(1f, 0.5f, 0.4f);   // red
            }
        }
    }
}
