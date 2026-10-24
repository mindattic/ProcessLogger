---
codex: 1
project: ProcessLogger
code: PL
layer: stories
status: living
updated: 2026-10-12
---
<!-- GENERATED from MindAttic.Tickets/data/Tickets.db by `tickets db render`. Do not edit: this file is rewritten from the database; change stories and tickets with the tickets CLI. -->

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
| PL-T10 | 2026-10-15 | chore | closed | Remove duplicate command copies (/quickload, /quicksave) now centralized at the user level (~/.claude/commands) | M.FLOW-T14 |
| PL-T11 | 2026-10-16 | chore | closed | install.ps1 adds this repo's 15 .prose/commands stubs for the shared global commands, so any LLM client finds them | mindattic-agent-standard/install.ps1 run 2026-10-16 |
| PL-T12 | 2026-10-16 | chore | closed | Bug hunt: run an exhaustive /bughunt over the whole repo and fix every real bug found (each fix under its own fix ticket), then hunt again; close this only when a full pass finds nothing | dd2a447 |
| PL-T13 | 2026-10-16 | chore | open | Run the full unit test suite and fix every failure (each under its own fix ticket) until it passes; put the passing run in --ref. No test project: close this wont-do saying so | user request 2026-10-16: approved repo sweep |
| PL-T14 | 2026-10-16 | chore | open | Clean up code and docs: remove dead code, stale comments and outdated or duplicated docs, and simplify where behaviour doesn't change (/simplify); tests stay green | user request 2026-10-16: approved repo sweep |
| PL-T15 | 2026-10-16 | docs | closed | Bring README.md up to date with the code as it is now (what it is, setup, usage, commands, current state), following codex-standard/README-STYLE.md; README.htm regenerated if the repo has one | a195fa9 |
| PL-T16 | 2026-10-16 | docs | closed | Update USER_STORIES.md | — |
| PL-T17 | 2026-10-19 | chore | wont-do | Convert tracked source files to LF line endings via .gitattributes, per HOUSE-LAW-13 (low priority) | — |
| PL-T18 | 2026-10-19 | fix | closed | Unused System.Security.Cryptography.Xml package reference in ProcessLogger.Core pulls in vulnerable System.Security.Cryptography.Pkcs 10.0.12, failing dotnet build/restore via NuGet audit errors | dotnet build ProcessLogger.slnx |
| PL-T19 | 2026-10-19 | fix | closed | PendingProcess.Merge keeps whichever source's Path arrives first instead of preferring ETW's, contradicting the documented reconciler behavior (README/class docs) | ProcessWatcherServiceTests.BothSources_WmiFirst_StillPrefersEtwPath |
| PL-T20 | 2026-10-19 | fix | closed | ProcessWatcherService.PersistAsync only catches ArgumentException around Process.GetProcessById(...).ProcessName/.MainWindowTitle, so Win32Exception/InvalidOperationException silently drop a process event and (in HandleStopAsync) skip MemorySamplerService.StopSampling, leaking the tracker | dotnet test ProcessLogger.slnx |
| PL-T21 | 2026-10-19 | fix | closed | FocusWatcherService.OnWinEvent only catches ArgumentException around Process.GetProcessById(...).ProcessName inside a native WinEvent hook callback; an uncaught Win32Exception/InvalidOperationException (e.g. PID 0 Idle process, protected/elevated foreground windows) can crash the app | manual code review, FocusWatcherService.cs |
| PL-T22 | 2026-10-19 | fix | closed | TestDbContextFactory uses Database.EnsureCreated() instead of Migrate(), so the test suite never exercises the real EF Core migrations that production applies at startup | dotnet test ProcessLogger.slnx |
| PL-T23 | 2026-10-19 | fix | closed | ProcessWatcherService fires unserialized concurrent SaveChangesAsync per process event, causing SQLite 'database is locked' failures under event bursts | a728da4 |
| PL-T24 | 2026-10-19 | docs | closed | Update USER_STORIES.md | aa77ecd |
| PL-T25 | 2026-10-19 | chore | closed | Archive the fully-landed MindAttic.Log/CSV-export quicksave now that /quickload has resolved it | 55589a9 |
| PL-T26 | 2026-10-20 | docs | open | Publish ProcessLogger docs to GitHub Pages via the shared Agency DocFX pipeline: run adopt-docs-site.ps1 (README.md as home page, docs/*.md as articles, API reference only where the code builds in CI), enable Pages with build_type=workflow, set the repo homepage URL, retire tools/build-readme.ps1 and README.htm, confirm the docs Actions run is green and https://mindattic.github.io/ProcessLogger/ returns 200. Depends on MAT-T8. | — |
| PL-T27 | 2026-10-22 | docs | closed | Update USER_STORIES.md | — |
| PL-T28 | 2026-10-23 | fix | open | The logger's MindAttic.Log database grows ~1.7 GB a day: C:\Users\ryand\AppData\Roaming\MindAttic\Logging\ProcessLogger\MindAttic.Log.db holds 1,067,472 Information rows from 19 hours (2026-10-19T23:27Z..2026-10-20T18:32Z), 641,389 of them EF Core 'Executed DbCommand' lines (Microsoft.EntityFrameworkCore.Database.Command logged at Information for every insert) and ~426,000 per-process '+ [Source] Name' / '- Name ran ...ms' lines that duplicate the ProcessEvents rows already in processlogger.db; raise Microsoft.EntityFrameworkCore to Warning and drop the per-process lines to Debug so the operational log holds only lifecycle, warnings and errors, with a test that a captured process start/stop writes no Information row to MindAttic.Log (found by audit M.ORCH-T657) | — |
| PL-T29 | 2026-10-23 | docs | closed | many billions of tokens | — |
| PL-T30 | 2026-10-24 | docs | closed | Update USER_STORIES.md | — |
