# Test and Verification Record

Date: 2026-11-16 · Command: `dotnet test RouteWatch.sln` → total 14, failed 0, succeeded 14, skipped 0.

## Automated cases

| Case | Test(s) | Result |
|---|---|---|
| TC-01 | TC01_ValidResponse_..., TC01_ResponseForDifferentBranch_... | Pass |
| TC-02 | TC02_HttpFailure_... | Pass (defect fixed) |
| TC-03 | TC03_InvalidJson_..., TC03_ValidJsonButMissingFields_... | Pass (defect fixed) |
| TC-04 | TC04_RepeatedAndUpdatedRefresh_... | Pass (defect fixed) |
| TC-05 | TC05_BranchQueriesReturnOnlyTheSelectedBranch | Pass (defect fixed) |
| TC-06 | TC06_FilterChanges..., TC06_SummaryCountsEveryStatus_... | Pass (defect fixed) |
| TC-07 | TC07_ExportWrites..., TC07_CommasQuotesAndNewlines_... | Pass (defect fixed) |
| TC-08 | TC08_SavedRecordsSurvive... | Pass |
| Extra | Ui_StartupConfigurationWarning_SurvivesTheInitialLoad | Pass (defect fixed) |

Each defect-related test was run first against the unfixed code and failed, then passed after the fix.

## Defects found and repaired

1. Non-2xx HTTP responses treated as success → `EnsureSuccessStatusCode()` (ShipmentService).

2. Invalid JSON returned 0 (silent success) → throws `InvalidDataException`.

3. Every refresh inserted duplicates → upsert by (branch, tracking number); never deletes.

4. Branch/tracking index was not unique → `.IsUnique()` (ShipmentDb).

5. `GetBranchAsync` ignored the branch → filtered by `BranchCode`.

6. InTransit summary counted Delivered → corrected.

7. CSV `Escape` did nothing → quotes, commas and newlines escaped.

8. Startup configuration warning was overwritten by the first load → `StartupNotice`.

## Manual acceptance

- **MA-01:** Delay test (10 s): status "Refreshing BR-001 using the 10-second delay test...", "Working..." shown, Load/Refresh/Export and the delay checkbox disabled, saved records stayed visible; finished with "Delay test succeeded for BR-001: 5 received." Service stopped: "Refresh failed: No connection could be made...", saved records and counts unchanged, nothing reported as success. Screenshots: evidence/ma01-*.png. (Add what you did to show the window stayed responsive, for example "opened the Status dropdown and moved the window during the wait".)

- **MA-02:** Published with `dotnet publish -r win-x64 --self-contained`. Folder check: exe, appsettings.json, coreclr.dll, System.Private.CoreLib.dll, e_sqlite3.dll, libSkiaSharp.dll, libHarfBuzzSharp.dll present. Launched by double-click outside Visual Studio: address prefilled from appsettings.json, saved records loaded at startup (persistence), Refresh for BR-001 returned 5, filtered CSV export matched the grid. Missing appsettings.json: status bar shows the configuration warning together with the saved-records message. Screenshots: evidence/ma02-*.png, evidence/config-warning-startup.png.

## Observations and incomplete items

- Two exports made earlier during testing showed timestamps 8 hours earlier than the grid. The database stored `+00:00` values, an in-app debug check showed `+00:00`, and later exports were correct (e.g. HUB-1001 = 2026-11-10T08:30:00Z). The earlier files could not be reproduced and were discarded.

- Clean-machine test (PC without .NET installed): NOT DONE

- Optional friendlier wording for HTTP errors was not implemented; the current messages were already clear in testing.

Screenshots: see [evidence/EVIDENCE.md](evidence/EVIDENCE.md).
