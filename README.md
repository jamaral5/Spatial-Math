# GraphCI Calc

A real-time 3D graphing calculator built in Unity and C#. Type in an equation and instantly see it rendered as an interactive, colored 3D surface you can rotate and explore. Click any point on the surface to see its slope and tangent plane — turning abstract calculus into something you can look at from every angle.

The long-term goal is a **virtual reality** version, so students with school-available headsets can explore multivariable math from the inside.

## Features

- **Type any equation** and watch it plot in real time (e.g. `sin(x)*cos(y)`, `x^2 - 2y`).
- **Custom-built equation parser** — reads typed math from scratch, with no external math libraries.
- **Procedurally generated surfaces** — every shape is calculated and built by code at runtime; nothing is pre-modeled.
- **Click-to-inspect calculus** — click a point to see its slope, a matching tangent plane, tangent lines, and the plane's equation.
- **Height-based coloring** — surfaces are shaded by height so peaks and valleys are easy to read.
- **Orbiting camera** to view the graph from any angle.

## Getting Started

**Requirements:** Unity `6000.3.9f1` (Unity 6.3) or compatible.

1. Clone the repository:
   ```bash
   git clone https://github.com/<your-username>/GraphCI-Calc.git
   ```
2. Open the project in Unity Hub (open the cloned folder).
3. Open the scene at `Assets/Scenes/SampleScene.unity`.
4. Press **Play**.

## Usage

- **Type an equation** into the input box and press **Enter** to plot it.
- **Use the preset buttons** for quick examples (sin·cos, paraboloid, saddle).
- **Left-click** anywhere on the surface to drop a marker and reveal the tangent plane, tangent lines, and equation at that point.
- **Orbit** the camera to view the surface from any angle.

### Supported syntax

- **Variables:** `x`, `y`
- **Operators:** `+  -  *  /  ^`  (implicit multiplication works too, e.g. `2y`, `3sin(x)`, `x(x+1)`)
- **Functions:** `sin, cos, tan, asin, acos, atan, sqrt, abs, exp, log, log10, ceil, floor, round, sign, pow(a,b), atan2(a,b), log(a,b)`
- **Constants:** `pi`, `e`

## How It Works

1. **Parse** — a hand-written recursive-descent parser turns the typed text into a reusable math function, respecting order of operations.
2. **Sample** — that function is evaluated across a grid of thousands of points spanning the graph's range.
3. **Build** — the sampled points become vertices, which are stitched into triangles to form a solid surface, and colored by height.
4. **Render** — the finished mesh is handed to Unity to draw, and copied into a collider so clicks can land on it.
5. **Inspect** — clicking estimates the surface's slopes at that point (via numerical differentiation) and draws the tangent plane and its equation.

## Project Structure

```
Assets/Scripts/
  EquationParser.cs        # Turns typed text into a math function
  GraphRenderer.cs         # Builds the 3D surface mesh from the function
  GraphManager.cs          # Manages equation slots and graph settings
  TangentPlaneRenderer.cs  # Slopes, tangent plane, tangent lines, equation label
  SurfaceSelector.cs       # Click-to-select a point on the surface
  AxisRenderer.cs          # Draws the X/Y/Z axes and labels
  CameraOrbiting.cs        # Orbiting camera controls
  UIManager.cs             # Buttons and equation input
```

## Roadmap

- [x] Preset equations
- [x] Custom typed equations
- [x] Tangent plane + equation on click
- [ ] Show symbolic derivatives (e.g. `2x` instead of a number)
- [ ] Multiple equations on screen at once
- [ ] Virtual reality environment

## Tech

Unity · C# · TextMeshPro · no external plotting or math libraries.

## License

Released under the [MIT License](LICENSE).
