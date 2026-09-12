# Repository Instructions

- For every substantial task, keep local working context in `.sandbox/research.md` and `.sandbox/plan.md`. Read both before implementation and update them when research, decisions, scope, or verification state changes.
- `.sandbox/` is local-only working state. Never commit its contents; keep the directory covered by `.gitignore`.
- Never read, inspect, or use an installed FFXIV game directory as generator/verifier input. Obtain inputs from a separate backup/export path, copy only the required files into `.sandbox/.tmp/`, and run all experiments against that staged copy.
- Before changing any lobby font, lobby ULD, data-center lobby, start-screen system settings, or character-select lobby code, read `Docs/LOBBY_FONT_REWORK_PLAN.md` and `Docs/LOBBY_ROUTE_SURVEY.md`.
- Treat previous lobby Hangul implementation code as reference only. Do not copy it back wholesale from `.tmp/lobby-rework-reference`.
- Lobby work must stay scoped to lobby fonts/assets unless a documented route proves otherwise. Do not modify in-game font repair logic to solve lobby-only issues.
