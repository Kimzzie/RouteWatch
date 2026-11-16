# Evidence

## Check 1: normal behavior
**Refresh BR-001.** Total 5, counts Pending 1 / In transit 2 / Delivered 1 / Exception 1.
![Refresh BR-001](check1-refresh-br001.png)

**Branch isolation.** BR-002 "Load saved" shows no BR-001 rows.
![BR-002 empty](check1-branch-isolation-br002.png)

**BR-002 refresh.** 2 shipments received.
![BR-002 refresh](check1-refresh-br002.png)

**CSV escaping.** Commas and quotes in fields are quoted correctly.
![CSV quoted fields](check1-csv-quoted-fields.png)

## MA-01: responsive external operations
**Delay test in progress.** "Working...", buttons disabled, saved records visible.
![Delay in progress](ma01-delay-working.png)

**Delay test finished.**
![Delay succeeded](ma01-delay-succeeded.png)

**Service stopped.** Failure reported, saved records unchanged.
![Service down](ma01-service-down.png)

## Defect: startup configuration warning
**Before the fix.** appsettings.json missing, but the status says everything is fine.
![Before](config-before-fix.png)

**After the fix.** The warning survives the initial load.
![After](config-after-fix.png)

## MA-02: published application
**Published exe launched outside Visual Studio.**
![Published launch](ma02-published-launch.png)

**Refresh and filter in the published exe.**
![Published refresh and filter](ma02-published-refresh-filter.png)

**Export from the published exe matches the grid.**
![Correct export](ma02-csv-export-correct.png)

**Publish folder contents.** Executable, configuration and native libraries are present.
![Publish folder check](ma02-publish-folder-check.png)