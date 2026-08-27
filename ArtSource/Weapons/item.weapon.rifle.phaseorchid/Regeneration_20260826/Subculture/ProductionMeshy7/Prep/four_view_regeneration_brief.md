# Phase Orchid Meshy 7 four-view regeneration brief

This preparation stage spends no Meshy credits. The current Subculture/TripoInput set remains preserved as evidence but is not approved for another paid reconstruction.

## Preserve

- One stylized subculture game rifle with an emerald-green, ivory and rose-gold body.
- Magenta energy is limited to the receiver orchid-eye core and narrow connected channels.
- Preserve the clean thumbhole stock, trigger grip, separate straight front support grip and long readable rifle silhouette.
- Preserve the orchid identity at the receiver and as shallow surface language near the muzzle.

## Simplify the failure-prone muzzle

- Use a short black cylindrical muzzle with a centered open bore.
- The black muzzle must protrude in front of every decorative part.
- Keep only one shallow connected sepal collar or four low-profile scallops behind the muzzle cut plane.
- The decoration must read as an orchid calyx from the side, but as a compact concentric ring from the front.
- The open bore must occupy 55-60 percent of the frontal muzzle diameter and retain a continuous circular or mechanically symmetric rim.
- No large blossom, thick radial petals, detached leaves, spikes or bright plates may surround or cross the bore.
- The front support grip must not appear as a vertical bar attached to the muzzle in FRONT.

## Four independent files

Generate four separate transparent PNG files, not a contact sheet:

1. `left.png` — strict orthographic left side; primary Meshy input.
2. `right.png` — exact horizontal mirror of the approved left unless a deliberate asymmetry is approved.
3. `front.png` — muzzle facing camera; open bore, shallow collar, no grip overlap.
4. `back.png` — stock facing camera; must not resemble the muzzle ring.

Recommended canvas is 2048×1024 RGBA with alpha 0 background. Use the exact same weapon scale, axis and center in every file. Do not include text, a character, hands, floor, shadow, aura or a checkerboard pattern.

## Meshy geometry-only request after approval

```text
ai_model: latest
model_type: standard
images: left.png, right.png, front.png, back.png
should_texture: false
should_remesh: false
image_enhancement: false
target_formats: glb
expected cost: 20 credits
```

The resulting raw GLB must pass the six-axis and muzzle-depth gates in `qa_gate.json` before remesh, texture generation, Blender marker work or Unity import.
