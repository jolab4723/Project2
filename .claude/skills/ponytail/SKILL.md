---
name: ponytail
description: Apply the repository's Ponytail minimal implementation or review pass to the current request only. Use only when the user explicitly invokes `/ponytail`, `/ponytail lite`, `/ponytail full`, `/ponytail ultra`, `$ponytail`, or clearly asks to apply Ponytail.
---

# Ponytail bridge

Read `.agents/skills/ponytail/SKILL.md` completely before acting and follow it as the canonical Ponytail instructions for the current request only.

Use `$ARGUMENTS` or the user's explicit wording to select `lite`, `full`, or `ultra`. If no level is provided, use `full`. After the request finishes, return to the normal `AGENTS.md` `lite` baseline.
