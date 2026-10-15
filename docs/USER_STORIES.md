---
codex: 1
project: ProcessLogger
code: PL
layer: stories
status: living
updated: 2026-10-12
---
<!-- GENERATED from MindAttic.Workflow/data/workflow.db by `workflow db render`. Do not edit: this file is rewritten from the database; change stories and tickets with the workflow CLI. -->

# ProcessLogger — User Stories
> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned. Every ✅ cites the test.

## Priority backlog
_Nothing recorded yet._

## Tickets
> Permanent log of every change and fix — this repo's ticket system. Rows are append-only: never
> delete or reword one; only move its Status (open → closed / wont-do) and fill in Ref. Status is
> a word, not a glyph, so tickets don't count toward the story index. Keep this section last.

| ID | Date | Type | Status | Summary | Ref |
|---|---|---|---|---|---|
| PL-T1 | 2026-10-12 | docs | closed | Start the permanent ticket log in USER_STORIES.md | — |
| PL-T2 | 2026-10-13 | chore | closed | Replace launch.bat with root deploy.ps1 (change-only republish to C:\Apps\ProcessLogger, generated deploy.bat) | deploy.ps1 |
| PL-T3 | 2026-10-14 | chore | open | NUnit tests for ProcessPathHeuristics.TryExtractPathFromCommandLine and PendingProcess.Merge (internal, visible to ProcessLogger.Tests): quoted path with spaces, unterminated quote returns null, leading whitespace, unquoted path up to first space, bare exe name or relative path returns null, null/blank returns null; Merge ORs SourceSeen and fills only null ParentProcessId/Path/CommandLine without overwriting - these decide the Path and Source columns every ETW/WMI row gets | — |
| PL-T4 | 2026-10-14 | chore | open | NUnit tests for TextFilter/TextFilterExtensions (Query/TextFilter.cs): IsSet false for null filter, null/empty/whitespace Text; Matches false for null value or unset filter; PartialText and ExactString case-insensitive; Regex case-insensitive and anchored only when the pattern says so; document behavior for an invalid regex pattern (currently throws RegexParseException into the query) - the filter box in the UI goes straight through this | — |
| PL-T5 | 2026-10-14 | chore | open | NUnit tests for ProcessEventQueryService gaps: sort by ClosedUtc and DurationMs both directions with null values, Name sort is case-insensitive on mixed-case names (the SQLite lower() workaround), ascending OpenedUtc, Exclude-only and Regex exclude, Take applied after in-memory include/exclude filtering keeps sort order, GetDistinctValuesAsync returns each value once - existing tests cover only partial/exact/regex include, simple exclude, Take and three sorts | — |
| PL-T6 | 2026-10-14 | chore | open | NUnit tests for FocusEventQueryService gaps: sort by ProcessName (case-insensitive, rows with null ProcessName) and FocusLostAtUtc both directions, ascending FocusedAtUtc, Exact and Regex include, null ProcessName never matches an include but survives an exclude, Take after filtering, GetDistinctValuesAsync - only partial include, exclude and one sort are tested today | — |
| PL-T7 | 2026-10-14 | chore | open | NUnit tests for ProcessWatcherService gaps (with FakeCaptureSource): EnabledSources=Etw ignores events from a WMI source, stop for a never-seen PID writes nothing, second source fills a null Path from the first, DurationMs equals stop minus open, memory sample fields are written on stop, after Stop/Dispose further raised events are ignored, repeated start for the same PID never yields two rows - the dedupe/reconcile logic is the core of the logger | — |
| PL-T8 | 2026-10-14 | chore | open | NUnit tests for MemorySamplerService: StopSampling for an unknown PID returns an empty sample with SampleCount 0, StartSampling the same PID twice keeps one tracker, sampling the current test process for ~200ms yields SampleCount>0 with PeakWorkingSet>=FinalWorkingSet and PeakPrivate>0, a non-existent PID yields an empty sample, Dispose cancels all trackers without throwing - peak memory columns come from here | — |
| PL-T9 | 2026-10-14 | chore | open | ProcessLogger.Frontend writes %TEMP%\processlogger-debug.log instead of MindAttic.Log; route it through MindAttic.Log at the Vault path | — |
| PL-T10 | 2026-10-15 | chore | closed | Remove duplicate command copies (/quickload, /quicksave) now centralized at the user level (~/.claude/commands) | WF-T14 |
