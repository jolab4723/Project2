# Retry2 input work record

- Date: 2026-08-27 (Asia/Seoul)
- Item: `item.weapon.grenadelauncher.apocalypse`
- Scope: retry input preparation and built-in ImageGen precise-object-edit only.
- Instructions read in full before editing:
  - `C:/Users/user/.codex/skills/.system/imagegen/SKILL.md`
  - `C:/Users/user/.codex/skills/.system/imagegen/references/prompting.md`
- Applied workflow: preserve `Concept20260827` unchanged; copy source views into `Retry2Input`; edit only the front muzzle structure first; keep orthographic framing, established silhouette, palette, proportions, and genuine transparent RGBA.
- Edit target: retain the non-circular violet fracture-prism exterior while replacing the shallow/blocked face with one clear, deep, dark firing tunnel and a continuous simple rim. Avoid a circular energy core or halo language associated with World Ender.
- Prohibited: checkerboard or opaque background, floor, cast shadow, aura, labels, floating parts, extra decoration.
- Meshy input order after QA: `left.png`, `right.png`, `front.png`, `back.png`.
- Result: front muzzle edited to a deep single tunnel; the side mismatch triggered one accepted left-side muzzle edit and an exact horizontal flip for `right.png`.
- Alpha result: ImageGen's painted RGB checkerboard was rejected; a deterministic connected-subject matte was used to produce true RGBA, remove exterior aura/checkerboard, and zero hidden RGB where alpha is zero.
- Input QA: PASS for the one authorized Meshy7 untextured Retry2 attempt.
