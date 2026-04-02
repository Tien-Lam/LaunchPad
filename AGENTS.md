# Agent Instructions

## Issue Tracking

This project uses **bd (beads)** for issue tracking.
Run `bd prime` for workflow context, or install hooks (`bd hooks install`) for auto-injection.

**PATH setup** (run first):
```bash
export PATH="$PATH:/c/Users/lamti/AppData/Local/Programs/bd:/c/Program Files/dolt/bin"
```

**Workflow -- every code change MUST follow this:**
```bash
bd create --title="..." --type=bug|task|feature --priority=2
bd update <id> --claim    # Atomic -- fails if already claimed by another agent
# ... write code ...
bd close <id>
```

**Do NOT use** TodoWrite, TaskCreate, or markdown task lists.

**Quick reference:**
```bash
bd prime                  # Full dynamic workflow context
bd ready                  # Find unblocked work
bd show <id>              # View issue details
bd worktree create <name> # Isolated worktree for parallel work
```

## Parallel Agents

When multiple agents work simultaneously:
- Each agent MUST `bd update <id> --claim` before starting -- this is atomic and prevents conflicts
- Use `bd worktree create <name>` for filesystem isolation (shared beads DB)
- Use `--actor <name>` to identify yourself in the audit trail
- Use `bd gate` for cross-agent coordination (block until another agent's work completes)
- Use `--readonly` flag in worker sandboxes that should not mutate issue state

## Non-Interactive Shell Commands

Always use non-interactive flags to avoid hanging:
```bash
cp -f source dest        # NOT: cp source dest
mv -f source dest        # NOT: mv source dest
rm -rf directory         # NOT: rm -r directory
```

<!-- BEGIN BEADS INTEGRATION v:1 profile:minimal hash:ca08a54f -->
## Beads Issue Tracker

This project uses **bd (beads)** for issue tracking. Run `bd prime` to see full workflow context and commands.

### Quick Reference

```bash
bd ready              # Find available work
bd show <id>          # View issue details
bd update <id> --claim  # Claim work
bd close <id>         # Complete work
```

### Rules

- Use `bd` for ALL task tracking — do NOT use TodoWrite, TaskCreate, or markdown TODO lists
- Run `bd prime` for detailed command reference and session close protocol
- Use `bd remember` for persistent knowledge — do NOT use MEMORY.md files

## Session Completion

**When ending a work session**, you MUST complete ALL steps below. Work is NOT complete until `git push` succeeds.

**MANDATORY WORKFLOW:**

1. **File issues for remaining work** - Create issues for anything that needs follow-up
2. **Run quality gates** (if code changed) - Tests, linters, builds
3. **Update issue status** - Close finished work, update in-progress items
4. **PUSH TO REMOTE** - This is MANDATORY:
   ```bash
   git pull --rebase
   bd dolt push
   git push
   git status  # MUST show "up to date with origin"
   ```
5. **Clean up** - Clear stashes, prune remote branches
6. **Verify** - All changes committed AND pushed
7. **Hand off** - Provide context for next session

**CRITICAL RULES:**
- Work is NOT complete until `git push` succeeds
- NEVER stop before pushing - that leaves work stranded locally
- NEVER say "ready to push when you are" - YOU must push
- If push fails, resolve and retry until it succeeds
<!-- END BEADS INTEGRATION -->
