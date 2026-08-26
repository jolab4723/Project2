# Echo Vault Unity Handoff QA

This folder is the final pre-Unity handoff package for the WebPreferred Echo Chamber production retry. The authoritative summary is `handoff_manifest.json`; the two contact sheets provide the visual design and PBR/emission gates.

## Unity texture mapping

- `UnityTextures/BaseColor.png`: Default texture, sRGB enabled, white material tint.
- `UnityTextures/Normal.png`: Normal Map texture, sRGB disabled.
- `UnityTextures/MetallicSmoothness.png`: Default texture, sRGB disabled, URP Lit Metallic Map. Use Metallic Alpha as Smoothness Source.
- `UnityTextures/Occlusion.png`: Default texture, sRGB disabled, URP Lit Occlusion Map.
- `UnityTextures/EmissionMask.png`: Default texture, sRGB disabled. Enable URP Lit emission and use the authored cyan target sRGB `(62, 217, 235)`, linear `(0.0481718242, 0.6938717613, 0.8307698768)`, with the reviewed Blender strength `2.5` as the initial visual reference.
- `UnityTextures/MOS.png`: optional packed alternative only for a shader whose contract is R Metallic, G Occlusion, B Smoothness. Do not assign MOS directly to the stock URP Lit Metallic Map.

Regenerate the Unity-ready textures with `Scripts/build_unity_packed_textures.py`. The script losslessly re-encodes the decoded BaseColor, Normal, and EmissionMask texels into genuine PNG containers, then reads the untouched Tripo ORM convention `R=Occlusion, G=Roughness, B=Metallic` and deterministically creates the three packed maps. The original source files and hashes remain unchanged. Run `Scripts/verify_handoff_package.py` afterward to verify all authoritative inputs, production files, decoded-texel equality, textures, renders, contact sheets, and JSON documents.

## Required handoff checks

- Preserve the single root and the `RightHandGrip`, `LeftHandGrip`, and `Muzzle` children from the FBX.
- Preserve `+Z` as muzzle direction and `+Y` as weapon up.
- Remap all source material slots to a non-null external URP/Lit material.
- Confirm the open muzzle, neutral stock, PBR response, and exact binary emission against the contact sheets before catalog or prefab registration.
- The optimized Tripo mesh has documented open boundaries. Backface-culling QA passes all five principal views, but Unity's final material/culling state must still be visually checked by root.

No Unity Assets, Phase Orchid files, or new Tripo task were changed by this handoff QA pass.
