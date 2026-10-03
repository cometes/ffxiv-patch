# Repository Instructions

- For every substantial task, keep local working context in `.sandbox/research.md` and `.sandbox/plan.md`. Read both before implementation and update them when research, decisions, scope, or verification state changes.
- `.sandbox/` is local-only working state. Never commit its contents; keep the directory covered by `.gitignore`.
- Never read, inspect, or use an installed FFXIV game directory as generator/verifier input. Obtain inputs from a separate backup/export path, copy only the required files into `.sandbox/.tmp/`, and run all experiments against that staged copy.
- Before changing any lobby font, lobby ULD, data-center lobby, start-screen system settings, or character-select lobby code, read `Docs/LOBBY_FONT_REWORK_PLAN.md` and `Docs/LOBBY_ROUTE_SURVEY.md`.
- Treat previous lobby Hangul implementation code as reference only. Do not copy it back wholesale from `.tmp/lobby-rework-reference`.
- Lobby work must stay scoped to lobby fonts/assets unless a documented route proves otherwise. Do not modify in-game font repair logic to solve lobby-only issues.

## Session Sandbox

- Every session must use its own task-specific directory under `.sandbox/`, such as `.sandbox/<session-name>/`. Choose a unique, descriptive session name and never reuse another session's directory.
- Store session notes at `.sandbox/<session-name>/research.md` and `.sandbox/<session-name>/plan.md`. Never create, read as the active workspace, or append to shared `.sandbox/research.md` or `.sandbox/plan.md` files.
- Keep every session-only artifact inside the same session directory. This includes temporary files, downloads, extracted archives, helper scripts, generated inputs, build products, test fixtures, test output, screenshots, logs, and any other material prepared for investigation or verification.
- Configure commands and tools so their working directories, download destinations, temporary directories, and output paths point inside `.sandbox/<session-name>/`; do not use shared paths such as `.sandbox/.tmp/`.
- Treat pre-existing shared files directly under `.sandbox/` as historical artifacts only. Do not modify or extend them for current work.
- Repository deliverables still belong in their normal tracked locations. Only scratch work, session notes, and temporary test or research material belong in the session sandbox.
