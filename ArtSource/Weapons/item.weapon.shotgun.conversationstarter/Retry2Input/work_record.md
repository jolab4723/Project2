# Retry2 input work record

- Date: 2026-08-27 (Asia/Seoul)
- Item: `item.weapon.shotgun.conversationstarter`
- Weapon type: Shockwave Shotgun
- Scope: retry input preparation and built-in ImageGen precise-object-edit only.
- Instructions read in full before editing:
  - `C:/Users/user/.codex/skills/.system/imagegen/SKILL.md`
  - `C:/Users/user/.codex/skills/.system/imagegen/references/prompting.md`
- Applied workflow: preserve `Concept20260827` unchanged; copy source views into `Retry2Input`; edit only the front muzzle structure first; keep orthographic framing, established silhouette, purple/gold palette, proportions, and genuine transparent RGBA.
- Edit target: retain the elegant trumpet-bell identity while replacing the shallow dish center with a separate, visibly deep cylindrical open bore, continuous simple rim, and recessed dark inner wall.
- Prohibited: checkerboard or opaque background, floor, cast shadow, aura, labels, floating parts, extra jewelry/wing/fan ornament.
- Meshy input order after QA: `left.png`, `right.png`, `front.png`, `back.png`.
- Result: trumpet bell preserved; its front center now reads as a separate deep cylindrical open shotgun bore. The existing long side neck remained usable, and `right.png` was replaced by an exact horizontal flip of the padded unclipped left view.
- Alpha result: ImageGen's painted RGB checkerboard was rejected; a deterministic connected-subject matte was used to produce true RGBA, remove exterior aura/checkerboard, and zero hidden RGB where alpha is zero.
- Input QA: PASS for the one authorized Meshy7 untextured Retry2 attempt.
