# Retry3 input work record

- Date: 2026-08-27 (Asia/Seoul)
- Item: `item.weapon.grenadelauncher.apocalypse`
- Authorization: one additional Retry3 after the Retry2 raw muzzle gate failure.
- Retry2 measured cause: positive-X muzzle center inset `0.0030748248` raw units, far below the `0.08 m` normalized acceptance threshold; visual QA showed an almost flush cap.
- Image workflow: built-in ImageGen `precise-object-edit`, using the fully read `imagegen/SKILL.md` and `references/prompting.md` rules already recorded in Retry2.
- Front correction: removed auxiliary holes and fine concentric detail; replaced the muzzle with one thick octagonal rim and broad stepped inner walls converging toward a deliberately transparent distant octagonal void.
- Side correction: replaced the final barrel with one long constant-diameter octagonal tube, one large side cutaway exposing the empty inner channel, and one open mouth. Cutaway and mouth negative spaces use alpha=0 to prevent a flat black cap interpretation.
- Alpha recovery: ImageGen's RGB background was converted to genuine binary RGBA; RGB is zero wherever alpha is zero; tiny disconnected matte specks were discarded; right is the exact flip of left.
- Input order: `left.png`, `right.png`, `front.png`, `back.png`.
- Input QA: PASS for the final authorized Apocalypse Meshy7 attempt.

