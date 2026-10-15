---
codex: 1
project: ProcessLogger
code: PL
layer: stories
status: living
updated: 2026-10-12
---
<!-- workflow-db: this repo's stories and tickets live in the MindAttic.Workflow database -->

# ProcessLogger — User Stories

This repo's stories and tickets (PL-US-…, PL-T…) live in the MindAttic.Workflow database
(`MindAttic.Workflow/data/workflow.db`), not in this file. Use the `workflow` CLI:

- Read: `workflow story list --repo ProcessLogger`, `workflow ticket list --repo ProcessLogger`, `workflow ticket show <ID>`
- Change: `workflow ticket new` / `close`, `workflow story status <ID> done|partial|planned`, `workflow ticket link <A> requires <B>`
- This file as it was, rebuilt from the database: `workflow db export --repo ProcessLogger`
