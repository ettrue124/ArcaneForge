# Claude Code handoff and shared memory

Prepared 2026-10-25. Claude Code 2.1.89 was found installed on this machine.

## Start

Open PowerShell:

```powershell
Set-Location 'C:UsersEthanDownloadsproject'
claude
```

Suggested first message:

> Read the shared project instructions and memory. Summarize the current state and the unverified fixes. Wait for my next task before changing gameplay code or restarting Arena.

Use `/memory` to inspect loaded instruction files. This setup has not launched a paid Claude session or verified loading interactively. Sign in through Claude's own prompt if needed; do not paste credentials into project notes.

## What is shared

- `AGENTS.md`: common working rules for both agents.
- `CLAUDE.md`: explicitly imports those rules and the short shared memory entry points.
- `.agent-memory/`: shared architecture, decisions, current status, workflow and new notes.
- `AI_SHARED_PROGRESS.txt`: existing detailed history, retained intact.

Both agents work with the same files in this checkout. This is durable local file memory, not a full chat-history migration or a background synchronization service. Agents must maintain the notes after material changes. Private auto-memory folders are not the shared source of truth.

The new files are eligible for Git tracking, but this preparation does not stage, commit or push anything. Save/commit them when you choose; copy the memory folder with the project when moving machines. Ignored runtime data and installed dependencies need separate migration.

## External memory / MemoryLake

No MemoryLake account, API key, MCP endpoint or upload was configured. The local shared memory works without them. If you want MemoryLake specifically, provide its official setup details/account connection and choose the documents to upload. Upload only curated project notes after review, not account databases, bootstrap configs, local tokens, traces or the entire workspace. An MCP connection in one agent does not automatically configure the other.

## Current documentation

Claude imports: https://code.claude.com/docs/en/memory
Codex project instructions: https://learn.chatgpt.com/docs/agent-configuration/agents-md

The pasted article is a third-party marketing source, not project policy. This handoff does not endorse its claims about context limits, lost session data, encryption or vendor capabilities.
