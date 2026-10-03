# Performance testing (load, stress and recovery)

This is how I load tested and stress tested the Gift of the Givers web app for Part 3, what I found, and what I changed because of it.

All times are South African time (SAST). The raw NBomber reports (HTML with graphs, CSV, Markdown) are written to `GiftOfTheGivers.LoadTests/reports/`. That folder is in `.gitignore`, so I kept copies in my POE evidence folder.

## Tools and setup

- **NBomber 6.6** in a console project, `GiftOfTheGivers.LoadTests`. It is in the solution, so it runs from Visual Studio: set it as the startup project and pick a profile in the launch profiles drop-down. It also runs from the command line:

  ```
  dotnet run -c Release --project GiftOfTheGivers.LoadTests -- --target live  --profile load
  dotnet run -c Release --project GiftOfTheGivers.LoadTests -- --target local --profile stress --label before
  dotnet run -c Release --project GiftOfTheGivers.LoadTests -- --target local --profile fault  --label after
  ```

- **Targets:**
  - `live` is the Azure web app (https://gift-of-the-givers-prototype-aweeh5f7g2czemdf.southafricanorth-01.azurewebsites.net).
  - `local` is a published Release build on http://localhost:5080, using a LocalDB database just for perf testing (`GiftOfTheGivers_PerfTest`).
- **Function key:** the tax certificate Function is only called when `GOTG_FUNCTION_KEY` is set as an environment variable. The key is never in the repo. The test asks for a donation id that does not exist (999999) and counts the 404 as a pass, so it never creates data.
- **Warm-up:** before every run the tool keeps calling the home page, the donate page and the Function until they answer in under 2 seconds. The Azure SQL database is serverless and auto-pauses, and the Function is on the consumption plan, so the first hit is slow (3.3 s for the Function and 0.6 s for the home page on my first try). I didn't want that in the results.
- **One user journey per iteration:** home → donate page → login page → (sign in as the demo donor → donor dashboard) → tax certificate Function (load profile only).
  - The load profile has 1 s of think time between steps. The stress and fault profiles have none.
  - Login and the dashboard only run when the demo donor can actually sign in on the target. That works locally, but not on the live site (see the limits below).
- **Raw data:** every request is also written to `raw-requests.csv`. I used it to make per-stage tables and graphs.

## Plan and environment I was testing

| Part | Tier | What it means for performance |
|---|---|---|
| Web app (App Service plan `ASP-Giftofthegiversprototypegroup-ab4b`) | **F1 Free** | Shared CPU, about 60 CPU-minutes a day, no Always On, so the app sleeps when idle. If the quota runs out, Azure stops the site for the rest of the day. |
| Azure SQL `free-sql-db-0536742` | Serverless GP_S_Gen5, auto-pause | Pauses when idle. The first connection after a pause waits for it to resume and can fail with transient errors (e.g. 40613). |
| Function App | Y1 consumption | Cold starts after it has been idle. |

Because of F1 I kept the live runs short: the load run was 2 min 40 s and the stress run 3 min 30 s. Azure Monitor shows the whole stress run used only about 24 s of CPU time, so the daily quota was never in danger.

## A.3 Load test (5 → 20 → 50 users)

Stages: 5 users (10 s ramp, 30 s hold), then 20 users (15 s ramp, 45 s hold), then 50 users (15 s ramp, 45 s hold). The numbers below are from the hold part of each stage.

**Live Azure site** (3 Oct 2026, 01:52–01:55, 3 552 requests, 0 errors)

| Users | Requests/s | p50 | p95 | p99 | Errors |
|---|---|---|---|---|---|
| 5 | 4.7 | 62 ms | 103 ms | 160 ms | 0 % |
| 20 | 18.1 | 66 ms | 301 ms | 611 ms | 0 % |
| 50 | 41.7 | 83 ms | 680 ms | 1 273 ms | 0 % |

- Per step over the whole run, p95 was 375 ms for home, 339 ms for the donate page and 342 ms for the login page.
- The tax certificate Function had a p95 of 1 102 ms. It is the slowest step because it is on the consumption plan.

**Local Release build** (same profile, with sign-in and the donor dashboard included, 4 065 requests, 0 errors)

| Users | Requests/s | p50 | p95 | p99 | Errors |
|---|---|---|---|---|---|
| 5 | 4.9 | 6 ms | 12 ms | 19 ms | 0 % |
| 20 | 19.7 | 5 ms | 18 ms | 42 ms | 0 % |
| 50 | 49.2 | 5 ms | 20 ms | 47 ms | 0 % |

- At 50 users, throughput grew in step with the number of users on both targets, so neither one was saturated at normal load.
- On the live site the p50 stayed under 100 ms, but the p95 and p99 went up a lot from 20 users. That is the shared F1 plan plus the internet trip, not the code: the same code locally stays at about 20 ms.

## A.3 Stress test (ramp 10 → 200 users, no think time)

Stages: 10, 25, 50, 75, 100, 150 and 200 users, each with a 10 s ramp and a 20 s hold.

The test watches a 10 s rolling window every 5 s and stops itself at the first clear degradation: more than 5 % errors, or a p95 over 5 s. That keeps the live site from being hammered once it is already struggling.

### Live site: bounded ramp (3 Oct 2026, 02:00:10–02:03:43)

| Users | Requests/s | p50 | p95 | p99 | Errors |
|---|---|---|---|---|---|
| 10 | 107 | 73 ms | 227 ms | 415 ms | 0 % |
| 25 | 112 | 117 ms | 705 ms | 2 435 ms | 0 % |
| 50 | 155 | 257 ms | 728 ms | 1 375 ms | 0 % |
| 75 | 162 | 419 ms | 861 ms | 1 087 ms | 0 % |
| 100 | 158 | 588 ms | 1 014 ms | 1 164 ms | 0 % |
| 150 | 186 | 810 ms | 1 047 ms | 1 126 ms | 0 % |
| 200 | 207 | 910 ms | 1 548 ms | 1 817 ms | 0 % |

**Degradation point:** at about **25–50 users** the site stops scaling.
- Going from 10 to 50 users (5 times more) only gave 1.45 times the throughput, and after that it stays at about 155–205 requests/s.
- Every extra user just waits longer: p50 went from 73 ms to 910 ms, and p95 went over 1 s at 100 users.
- There were **no errors** (no 5xx, no timeouts) all the way to 200 users. The guard never fired, so the run ended at the top of the bounded ramp.

**Azure Monitor during the run** (`az monitor metrics list`, 1-minute grain):
- Requests peaked at about 10 500 per minute.
- **CPU time** peaked at **10.3 s per minute**, which is about 17 % of one core.
- **Server response time** stayed at about **11–13 ms on average** (max 96 ms).
- **HTTP 5xx: 0**, HTTP 4xx: 0.
- Memory went from 200 MB to 229 MB.
- SQL CPU peaked at 6 %.

So the app and the database were both nearly idle while the client saw nearly 1 s. The queueing happens in front of my code: the shared F1 front end, plus the internet path from my PC to South Africa North. Little's law fits this: 200 users ÷ about 0.9 s ≈ 220 requests/s, which is the ceiling we saw. To split the platform from the network I would have to run the test from inside Azure (e.g. Azure Load Testing), which the free tier doesn't give me.

### Local Release build: before and after the mitigation

I did the before/after comparison locally, not live, for two reasons:
- The live site is deployed by a separate GitHub Actions workflow from the GitHub `master` branch. Pushing the mitigation to live needs that deploy, which is outside this pipeline and not something I could do as part of this task.
- Locally I can push the app until the *app* is the bottleneck, instead of the F1 front end or my internet line.

The client and the server run on the same PC here, so the absolute numbers are lower than a real server would give. Both runs used the same machine, the same database and the same profile, so the comparison is fair.

**Before:** the home page is the bottleneck. At 200 users the home page p50 was 305 ms, while the donate and login pages were 5 ms. The home page runs 5 database queries on every visit (totals and recent activity) and nothing was cached.

## Mitigation (what I changed)

1. **Output caching on the home page** (`[OutputCache(PolicyName = "PublicPage")]`, 60 s).
   - Only anonymous GET requests are cached. Signed-in users and anything that sets a cookie are never cached, so nobody sees someone else's page.
   - The donate and login pages are not cached because they have antiforgery tokens.
   - The integration tests in `OutputCachingTests` prove all three of these rules.
2. **SQL retry for transient faults:** `EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: 10 s)`. This is for the serverless database resuming or failing over.
   - In the fault test the dropped connection came back as error **-1** ("physical connection is not usable"), which EF Core does not treat as transient by default, so I added `-1` to `errorNumbersToAdd`.
   - The app has no manual transactions, so the retry strategy is safe to switch on.

### Effect on the same stress profile (local)

| Users | Requests/s before → after | p50 before → after | p95 before → after | Home page p95 before → after | Errors |
|---|---|---|---|---|---|
| 10 | 610 → 763 | 13 → 6 ms | 43 → 39 ms | 57 → 8 ms | 0 / 0 |
| 25 | 688 → 948 | 25 → 10 ms | 100 → 86 ms | 122 → 13 ms | 0 / 0 |
| 50 | 629 → 966 | 48 → 12 ms | 229 → 184 ms | 282 → 15 ms | 0 / 0 |
| 75 | 587 → 969 | 72 → 12 ms | 382 → 283 ms | 471 → 17 ms | 0 / 0 |
| 100 | 653 → 914 | 85 → 13 ms | 432 → 412 ms | 511 → 16 ms | 0 / 0 |
| 150 | 653 → 915 | 130 → 13 ms | 655 → 629 ms | 738 → 24 ms | 0 / 0 |
| 200 | 640 → 925 | 176 → 12 ms | 890 → 831 ms | 977 → 16 ms | 0 / 0 |

- Over the full 3.5-minute run the app did **184 260 requests after vs 129 290 before (+43 %)**.
- The home page p50 fell from 305 ms to **7 ms**.
- The overall p95 only improved a little. With the home page out of the way, the next bottleneck shows up: the **donor dashboard and login**. Login hashes the password with PBKDF2 (10 000 iterations) and the dashboard runs per-user queries. Neither can be output cached because they belong to one user. That would be the next thing to look at, e.g. caching the dashboard totals per user for a short time.

### Recovery test: database dies under load (fault profile)

25 users with no think time for 60 s. At **t = 20 s** the test kills the LocalDB instance (`sqllocaldb stop MSSQLLocalDB -k`). LocalDB starts again on the next connection, much like a serverless database resuming.

| Build | Requests | Failed | Errors seen |
|---|---|---|---|
| Before (no cache, no retry) | 23 981 | **14** (0.058 %) | 10 × HTTP 500, 4 × login failed ("an error occurred") |
| After v1 (cache + EF default retry list) | 56 623 | **25** (0.044 %) | 18 × HTTP 500, 7 × login failed. Error -1 was not retried. |
| **After (cache + retry incl. -1)** | 42 155 | **0** | none. The slowest requests took about 4.2 s while they waited for the retry. |

The server log during the before run showed `SqlException ... A transport-level error has occurred ... Physical connection is not usable` (Error Number -1, Class 20). With the final retry settings, users see a slow request instead of an error page, and the cached home page keeps working the whole time.

## Bottlenecks in plain words

1. **Serverless SQL cold start / auto-pause:** the first visitor after the database has been idle waits for it to resume, or used to get an error. The warm-up step and the retry logic cover this. Turning off auto-pause would cost money.
2. **F1 Free plan:** shared CPU, a daily CPU quota, and no Always On, so there are app cold starts too. Under stress, throughput levelled off at about 200 requests/s while my app was still nearly idle. A Basic B1 plan (dedicated core, Always On) is the obvious upgrade when it is needed.
3. **Consumption-plan Function:** a 3.3 s cold start and the highest p95 (about 1.1 s) at normal load.
4. **Home page doing 5 queries per visit:** fixed with output caching.
5. **Login (PBKDF2) and the donor dashboard:** now the slowest steps under heavy local load. That is expected and acceptable for a secure login, but it is the next place to optimise.

## Limits of these tests (being honest)

- The demo accounts (`donor@test.local`, `employee@test.local`) don't sign in on the live site because their passwords there are different. I didn't create new accounts on live, so login and the dashboard were only load tested locally.
- The live numbers include my home internet connection. The local numbers share one PC between the client and the server.
- The NBomber free licence is for personal / study use only.
