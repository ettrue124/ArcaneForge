# Shared project instructions

These are the common working rules for Codex and Claude Code in this repository.

## Start every session

Read `.agent-memory/INDEX.md` and `.agent-memory/CURRENT.md` before changing code. Read the relevant topic file from the index. Memory is evidence and context; the user's current request takes precedence. Recheck source and runtime state before relying on old notes.

## Working agreements

- Preserve the existing working tree. It contains extensive intentional modified and untracked work. Never reset, clean, discard, stage everything, commit or push without a user request.
- Do not add or run tests unless the user asks. Builds and read-only diagnosis are allowed. A successful build is not gameplay or visual confirmation.
- Communicate briefly in plain language. Diagnose a reported symptom before changing rules; distinguish actual rules behavior, AI decisions, display faults and hidden information.
- Do not spawn subagents unless the user or an applicable instruction explicitly requests delegation.
- Keep unrelated tasks and files intact. Do not change or restart the game for documentation-only work.
- Before a runtime restart, tell the user that it ends the current match. Use `Restart-Arena.ps1` for authorized restarts; do not improvise broad process kills.
- Never expose credentials, tokens, account databases, bootstrap secrets or full process command lines in output, memory or external services.
- Do not manually call `/api/interactive/poll`; it drains events used by the active client.
- On Windows use native PowerShell file operations and literal paths. Check recursive deletion/move targets before acting. Launch helpers hidden.

## Memory maintenance

Use `.agent-memory/` as the shared source of project knowledge, not an agent-private memory folder. Update `CURRENT.md` after material work with what changed, why, build/test evidence and next checks. Add durable rationale to the appropriate topic file. Append concise dated records to `AI_SHARED_PROGRESS.txt`; preserve its history. Mark hypotheses and unverified results explicitly. Do not copy raw logs or secrets into memory.

See `.agent-memory/WORKFLOW.md` for commands and diagnosis, `.agent-memory/DECISIONS.md` for pitfalls, and `docs/CLAUDE-HANDOFF.md` for onboarding.
