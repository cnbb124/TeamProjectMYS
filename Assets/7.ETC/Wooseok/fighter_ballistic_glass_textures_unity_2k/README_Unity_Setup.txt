Fighter Ballistic Cockpit Glass Texture Set for Unity
====================================================

Resolution:
- 2048 x 2048 PNG textures

Files:
- T_BallisticGlass_AlbedoAlpha_2K.png
  Cool blue glass tint. Alpha channel contains transparency.

- T_BallisticGlass_Normal_2K.png
  Subtle laminated-glass waviness, micro scratches, and small surface imperfections.

- T_BallisticGlass_Roughness_2K.png
  Roughness map. White = rougher, black = smoother.
  For Unity Built-in Standard shader, you usually need Smoothness instead of Roughness.

- T_BallisticGlass_MetallicSmoothness_2K.png
  RGB = near-zero metallic. Alpha = Smoothness.
  Recommended for Unity Built-in Standard Metallic workflow.

- T_BallisticGlass_SpecularSmoothness_2K.png
  RGB = cool blue specular color. Alpha = Smoothness.
  Useful if you use the Standard Specular setup.

- T_BallisticGlass_Emission_2K.png
  Optional subtle blue rim/glint emission.

- T_BallisticGlass_OpacityMask_2K.png
  Separate alpha/opacity mask.

- T_BallisticGlass_Height_2K.png
  Optional height map for custom shaders or detail work.

Suggested Unity Built-in material settings:
1. Shader:
   - Standard
   - Rendering Mode: Transparent or Fade
   - Metallic: 0
   - Normal Map: T_BallisticGlass_Normal_2K.png

2. Maps:
   - Albedo: T_BallisticGlass_AlbedoAlpha_2K.png
   - Metallic: T_BallisticGlass_MetallicSmoothness_2K.png
   - Normal: T_BallisticGlass_Normal_2K.png
   - Emission: optional T_BallisticGlass_Emission_2K.png

3. Recommended values:
   - Albedo color tint: light cyan / pale blue
   - Metallic: 0
   - Smoothness: 0.78 ~ 0.92
   - Alpha: 0.25 ~ 0.55 depending on cockpit thickness
   - Normal strength: 0.15 ~ 0.35
   - Emission intensity: very low, around 0.05 ~ 0.2

Notes:
- This set is designed for fighter cockpit / canopy ballistic glass, not ordinary window glass.
- The alpha and edge-darkened tint are meant to imply thick laminated material.
- Use a separate mesh shell or duplicated canopy mesh if you want thick rim refraction-like layering in Built-in RP.
