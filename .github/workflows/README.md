# GitHub Actions workflows

This folder holds the automation that GitHub runs for Brainova. Each `.yml` file is one **workflow**.

| File | What it does | Runs on |
|---|---|---|
| [`ci.yml`](ci.yml) | Restores, builds and tests the whole solution | push to `develop` / `main`, every pull request, manual button |
| [`secret-scan.yml`](secret-scan.yml) | Scans the git history for committed passwords and keys (gitleaks) | every push and pull request |

The rest of this document explains `ci.yml`.

---

## What CI is and why this repo has it

**Continuous Integration** means: every change is built and tested automatically, on a machine that is not yours.

Without it, "it works" means "it works on my laptop, with my Visual Studio, my cached packages and my user-secrets". With it, every commit is checked on a clean machine that has nothing except what is in the repo. That catches:

- a file that was never added to git, so the project only compiles locally
- a teammate's merge that breaks a service someone else wrote
- a test that used to pass and quietly stopped passing
- a path that only works on Windows because Windows ignores upper/lower case

The result shows up as a green check or a red X next to each commit and on each pull request.

---

## The vocabulary

| Term | Meaning |
|---|---|
| **Workflow** | One `.yml` file. A recipe GitHub follows when something happens. |
| **Event / trigger** | The "something happens": a push, a pull request, a button click. Declared under `on:`. |
| **Job** | A group of steps that run on one machine. `ci.yml` has one job, `build-and-test`. |
| **Step** | One action inside a job: either a shell command (`run:`) or a reusable action (`uses:`). |
| **Runner** | The temporary virtual machine GitHub lends you. It starts empty and is deleted when the job ends. |
| **Action** | A reusable step published by someone else, e.g. `actions/checkout`. The `@v4` is its version. |
| **Artifact** | A file saved from the runner before it is deleted, downloadable from the run page. |

---

## What happens in one run

```
push / pull request
        |
        v
+---------------------------- ubuntu-latest runner ----------------------------+
| 1. Checkout         clone the repo at the commit that triggered the run      |
| 2. Setup .NET 10    install the SDK                                          |
| 3. Cache            put back ~/.nuget/packages from the previous run         |
| 4. Restore          dotnet restore Brainova.slnx                             |
| 5. Build            dotnet build   --configuration Release --no-restore      |
| 6. Test             dotnet test    --configuration Release --no-build        |
| 7. Upload results   save TestResults/*.trx as an artifact (even on failure)  |
+------------------------------------------------------------------------------+
        |
        v
green check  or  red X  on the commit / pull request
```

Steps run top to bottom. The first one that fails stops the job; later steps are skipped, except step 7, which is marked `if: always()`.

---

## The file, section by section

### `on:` — when it runs

```yaml
on:
  push:
    branches: [develop, main]
  pull_request:
  workflow_dispatch:
```

- **`push` to `develop` or `main`** — the two long-lived branches. `develop` is where daily work lands; `main` receives `develop` through a pull request.
- **`pull_request`** — any PR, from any branch. This is what puts the check on the PR page.
- **`workflow_dispatch`** — adds a *Run workflow* button in the Actions tab.

Feature branches are deliberately not under `push`. A feature branch with an open PR is already tested by the `pull_request` trigger; listing it under `push` as well would test every commit twice.

### `permissions:` — least privilege

```yaml
permissions:
  contents: read
```

Each run gets a temporary token. CI only reads code, so the token is limited to read-only. If a dependency were ever compromised, it could not push to the repository.

### `concurrency:` — cancel outdated runs

```yaml
concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true
```

Push twice within a minute and the first run is cancelled: its result is about a commit that is no longer the latest. The group name is built from the workflow name and the branch, so runs on `develop` never cancel runs on `main`.

### `env:` — shared variables

```yaml
env:
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true
  SOLUTION: Brainova.slnx
  CONFIGURATION: Release
```

The first two keep the logs short and switch off telemetry. `SOLUTION` and `CONFIGURATION` are used by the restore, build and test steps, so each is written once.

### `runs-on` and `timeout-minutes`

```yaml
runs-on: ubuntu-latest
timeout-minutes: 15
```

Ubuntu runners start fastest, and .NET runs the same on Linux. The timeout stops a hung job after 15 minutes instead of GitHub's default of 6 hours.

### The steps

| # | Step | Why it exists |
|---|---|---|
| 1 | `actions/checkout@v4` | The runner is empty. This clones the repo. |
| 2 | `actions/setup-dotnet@v4` with `10.0.x` | All four projects target `net10.0`. `10.0.x` takes the newest patch release. |
| 3 | `actions/cache@v4` | Reuses downloaded NuGet packages between runs (details below). |
| 4 | `dotnet restore` | Downloads packages. Separate step so a package problem is not mistaken for a code problem. |
| 5 | `dotnet build --no-restore` | Compiles DAL, BLL, PL and Tests in `Release`. `--no-restore` avoids repeating step 4. |
| 6 | `dotnet test --no-build` | Runs the xUnit tests against the DLLs from step 5 and writes a `.trx` report. |
| 7 | `actions/upload-artifact@v4` | Keeps the report after the runner is deleted. Runs even when tests fail. |

### How the NuGet cache works

```yaml
path: ~/.nuget/packages
key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
restore-keys: |
  ${{ runner.os }}-nuget-
```

- **`path`** is the folder NuGet downloads into.
- **`key`** is the cache's name. It includes a hash of every `.csproj`, so it changes whenever a package is added, removed or upgraded.
- **`restore-keys`** is the fallback: if no cache has exactly that key, use the newest one whose name starts with `Linux-nuget-`. Restore then downloads only what is missing, and a new cache is saved under the new key.

So: same packages as last time means nothing is downloaded; one package changed means only that one is downloaded.

---

## Why the build needs no secrets

The connection string, JWT key and SMTP password are read when the API **starts** (`Program.cs`, `EmailSetting.cs`, `AuthenticationService.cs`). CI compiles the code and runs unit tests; it never starts the API, and the tests replace the database with Moq and MockQueryable. So no secrets are stored in GitHub for this workflow.

This changes the day an integration test boots the real app with `WebApplicationFactory`. That test will have to supply its own test values for those settings.

---

## Reading the result

- **On a commit** — a green check or red X beside the commit message. Click it for the list of checks.
- **On a pull request** — the *Checks* section at the bottom shows `ci / build and test`.
- **Actions tab** — every run, with the log of each step. Click a failed step to expand its output.
- **Artifacts** — at the bottom of a run's page, `test-results` contains the `.trx` file. Open it in Visual Studio to see each test's result.

### When it fails

| Failed step | Usual cause | What to do |
|---|---|---|
| Restore | A package version that does not exist, or nuget.org was unreachable | Check the version in the `.csproj`; if it was the network, press *Re-run jobs* |
| Build | A compiler error (`CS....`), often a file that exists locally but was never committed | Run `git status`, then `dotnet build -c Release` locally |
| Build, "file not found" only in CI | A folder or file name with different upper/lower case than the code uses | Make the name match exactly; Linux treats `Services` and `services` as different |
| Test | A test assertion failed | Read the test name in the log, run `dotnet test` locally |

---

## Run the same thing locally

These are the three commands CI runs. If they pass on your machine, CI should pass too.

```bash
dotnet restore Brainova.slnx
dotnet build   Brainova.slnx --configuration Release --no-restore
dotnet test    Brainova.slnx --configuration Release --no-build
```

Stop the running API first. Otherwise the build reports `MSB3021` / `MSB3027` "file is locked" errors, which are about the running process, not about the code.

---

## Changing the workflow

| I want to... | Change |
|---|---|
| Run CI on another branch's pushes | Add it to `on.push.branches` |
| Move to a newer .NET | `dotnet-version` in the *Setup .NET* step, plus `TargetFramework` in the four `.csproj` files |
| Fail the build on compiler warnings | Add `-warnaserror` to the *Build* step |
| Collect code coverage | Add `--collect "XPlat Code Coverage"` to the *Test* step (`coverlet.collector` is already referenced) |
| Block merging while CI is red | GitHub: *Settings → Branches → Add rule →* require the `build and test` status check |
| Show a status badge in the main README | `![ci](https://github.com/AbdalruhmanIssa/Brainova/actions/workflows/ci.yml/badge.svg?branch=develop)` |
