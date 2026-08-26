# Built-in image generation prompt record

Taxonomy used: `stylized-concept`.

## Shared design brief

- A short, simple, readable subculture SF energy rifle for Gunner.
- Warm-white ceramic/polymer shell, vivid red secondary panels, graphite grips/structure.
- Only one cyan-teal emission color family.
- Flat chibi Gunner face decal: short white hair, red eyes, tiny red ornament; no words; no raised geometry; no white emission.
- No excessive industrial attachments, spikes, cables, hoses, floating parts, hand-space obstructions, character, hands, floor, shadow, text, logo, or watermark.
- Trigger-grip center to fore-end contact center targeted at approximately 650 image pixels; clean support corridor targeted at approximately 180 pixels.

## Native-alpha anchor request

The successful left-alpha attempt began and ended with the native transparency requirement and requested:

> Create one production game-asset reference image as a real transparent PNG cutout. Exact LEFT SIDE 90° orthographic view of one compact anime-game styled sci-fi energy rifle; muzzle right, stock left, barrel horizontal; warm-white/red/graphite palette with one cyan-teal strip; small flat chibi face receiver sticker; 2048×1024 RGBA; alpha exactly 0 outside the weapon and inside openings; no checkerboard, background, floor, shadow, halo, text, or perspective.

## Right 270° request

The right request referenced `left_native.png`, repeated `NATIVE ALPHA-0 TRANSPARENCY REQUIRED; no checkerboard pattern, not a transparency preview` at the first and last line, and required the exact opposite orthographic direction, exact preserved scale/parts, muzzle left, stock right, and the same bilateral flat decal position. Result: RGB checkerboard, rejected.

## Front 0° request

The front request referenced `left_native.png`, repeated the same native-alpha sentence at the first and last line, and required a true narrow end-on muzzle-axis projection, centered open bore, symmetric white/red muzzle prongs, stock and trigger grip receding behind it, weapon top screen-up, no side profile or 3/4 interpretation. Result: visually correct open bore but RGB checkerboard, rejected.

## Explicit non-action

No prompt was sent for rear 180° after the repeated RGB failure triggered the stop gate. No CLI/API fallback or alternate image model was used.
