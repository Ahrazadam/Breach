# UI/Emboss HDRP Shader Usage

This folder already contains the ready-to-use shader asset (`UIEmbossHDRP.shader`). You only need to copy the `Shaders` folder into your Unity project (anywhere inside the `Assets/` tree is fine, e.g., `Assets/Breach/Shaders`). Unity will auto-import the shader as soon as it is under `Assets/`.

Follow these steps to use the `UI/Emboss HDRP` shader with a Unity UI Image or a TMP/Sprite element in HDRP:

1. **Create a material**
   - In the Project window, right-click and choose **Create ▸ Material**.
   - Name it (e.g., `UI Emboss Mat`) and set the **Shader** drop-down to **UI/Emboss HDRP**.

2. **Assign your sprite**
   - Drag your sprite (or sprite atlas sub-asset) into the material’s **Sprite (_MainTex)** slot.
   - Optional: enable **Use Alpha Clip** if you want the sprite’s transparent pixels to be fully clipped for stencil/interaction consistency.

3. **Tune the surface look**
   - **Embossed (On) / Engraved (Off)**: turn on for a raised look, off for a carved look.
   - **Depth**: increases the strength of the normal-from-height effect; higher values exaggerate the lighting relief.
   - **Light Direction**: tweak to change where the highlight/shadow fall; it’s a vector (x,y,z) interpreted in view space.
   - **Base/Highlight/Shadow Colors**: set the underlying surface color and the two-tone lighting ramp the shader uses when shading from the heightmap-derived normal.
   - **Tint (Color)**: multiplies the final color (alpha-inclusive) so you can color-tint per material or per-Image color.

4. **Apply to UI elements**
   - Select your UI **Image** (or other Graphic) component.
   - Drag the material onto the **Material** field of the component.
   - Keep the **Source Image** assigned for correct UVs/alpha; the shader uses the sprite alpha as its height map.

5. **Stencil/UI settings**
   - The material exposes standard UI stencil properties (**Stencil ID/Comp/Op/Read/Write** and **Color Mask**) so you can integrate with existing UI masks and stencil setups.

6. **(Optional) Assign at runtime**
   - Create the material in the Project window as above.
   - Reference it from a script and assign it to your Image’s `material` field, e.g.:
     ```csharp
     // using UnityEngine.UI;
     public Image target;
     public Material embossMat;

     void Start() => target.material = embossMat;
     ```
   - Make sure the material remains under `Assets/` so it is included in builds.

## Notes
- The shader samples neighboring pixels to compute a normal from the sprite alpha, then lights it using the supplied light direction and three-color ramp for highlight, mid/base, and shadow.
- Because it uses HDRP’s unlit UI pass, keep the canvas in **Screen Space - Overlay/Camera** or **World Space**; the effect is entirely texture-driven and does not rely on scene lights.
- SRP Batcher compatibility: properties are defined in `UnityPerMaterial`, so materials are SRP-batcher-friendly in HDRP.
- If the shader is not selectable in the material’s Shader drop-down, ensure the `.shader` file sits under `Assets/` in your project and let Unity reimport (right-click the shader and choose **Reimport** if needed).
