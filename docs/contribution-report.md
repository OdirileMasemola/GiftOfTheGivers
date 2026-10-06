# Team Contribution Report (APPR6312 POE Part 3, Section D.2)

Odirile Masemola (ST10441421). Azure Repos branch `main` (mirrored to GitHub `final-poe`), tip `e2a0606` on 7 October 2026.

## Part 3 roles (all by Odirile Masemola)

| Area | What was done |
|---|---|
| Developer | Logout / Operations fixes, output caching, SQL retry, footer build number, recurring donations, printable tax certificate, wider volunteer form |
| Tester | 113 unit, 41 integration, NBomber load/stress, 16 Selenium UI tests (desktop + mobile) |
| DevOps / CD | Azure Pipelines YAML: build, test, coverage, SQL migrations, Function + web deploy, smoke tests, rollback |
| Documentation | Performance report, deployment strategy, user manual, this report |

Commits use Git names **Tech Grootman**, **Odirile Masemola** and **OdirileMasemola** (same email).

## Git evidence

`azure/main` has **88** commits; **57** are mine. Every commit from 3–7 October (Part 3 testing, pipeline, deployment, handover) is mine. Earlier history also includes other contributors (honest summary from `git log`):

| Git author | Commits | Work (from messages) |
|---|---|---|
| Odirile Masemola / Tech Grootman / OdirileMasemola | 57 | Setup, UI, login, Functions, CI/CD, all Part 3 |
| RtCzee / Didintle Mokgoro | 14 | Home, employee dashboard, operations, volunteer review (18 Aug–24 Sep) |
| Sibusiso Mabena / Sbue_05 | 8 | Volunteer management (25 Aug–25 Sep) |
| Styzey._ | 4 | Public volunteer application (23–24 Sep) |
| RatoM / leratomokoe | 3 | README, employee donation reporting |
| Alexis | 2 | Volunteer dashboard bound to DB (21–24 Sep) |

## Key Part 3 commits

| Hash | Date | Commit |
|---|---|---|
| 6491acd | 22 Sep 2026 | ci: add unit tests and Azure Pipelines restore/build/test |
| 6ecf68d | 3 Oct 2026 | Run unit and integration tests with coverage in the pipeline |
| 5c3cf6a | 3 Oct 2026 | Add NBomber load and stress tests, output caching and SQL retry |
| d463e26 | 3 Oct 2026 | Add Selenium UI tests for desktop and mobile |
| c5f1b28 | 6 Oct 2026 | Add continuous deployment for web app, Function and SQL |
| d4eb401 | 6 Oct 2026 | Add recurring donations and a printable tax certificate |
| 2efa812 | 6 Oct 2026 | Make the donation frequency optional on post (fixes failed run 23) |
| e2a0606 | 7 Oct 2026 | Widen and tidy the volunteer form |

Full Word one-pager: `APPR6312 POE Part 3 - Team Contribution Report.docx`.
