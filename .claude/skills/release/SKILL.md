---
name: release
description: Cut a ShipWar Android release end to end. Pre-flight checks, Level 7 smoke test, signed AAB build (with antivirus retry), signature/version verification, Google Play upload, track check, version commit + v<version> tag + push. Use only when the user explicitly asks to release / publish / ship a new build.
argument-hint: "[version e.g. 0.9.1] [internal|alpha|beta|production]  (both optional)"
disable-model-invocation: true
---

# Release ShipWar

Arguments: `$ARGUMENTS`. Both parts are **optional** and can come in any order:

| Usage | Version | Track |
|---|---|---|
| `/release` | auto: patch bump of the current `bundleVersion` (0.9.0 → 0.9.1) | `internal` |
| `/release 0.10.0` | `0.10.0` | `internal` |
| `/release beta` | auto patch bump | `beta` |
| `/release 1.0.0 production` | `1.0.0` | `production` |

Parsing rules:
- A token matching `^v?\d+\.\d+(\.\d+)?$` is the version name. Strip a leading `v`, and turn `1.2` into `1.2.0`.
- One of `internal | alpha | beta | production` is the track. Any other token: stop and ask.
- A version you were given must be **greater** than the current `bundleVersion` (semver compare). Its `v<name>` tag must not already exist. Otherwise stop and ask.
- With no version given, use the auto patch bump and show it in the step 1.5 confirmation, so the user can still change it there. Don't ask a separate question.
- The versionCode is never an argument. It is always computed (step 1.4).

Fixed facts (see also `CLAUDE.md` → batch mode / Signed Android AAB build):

| Thing | Value |
|---|---|
| Unity | `C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe` |
| Project | `C:\project\battle-ship` |
| Package | `com.mataco.seewargame1` |
| Keystore / alias | `C:/project/key.keystore` / `ship`. Passwords in `C:\project\keystore.txt` (line 1 store, line 2 key) |
| Signing cert SHA-256 | `A2:8C:03:BC:1C:A8:A6:D4:87:5B:F7:AF:C9:E4:2C:48:5A:92:C1:CD:C8:43:B0:92:D2:54:B6:54:DA:FE:27:7E` |
| Play key | `C:\project\play-service-account.json` |
| Python venv (Google libs) | `C:\project\.playvenv\Scripts\python.exe`. If missing: `python -m venv C:\project\.playvenv` + `pip install google-api-python-client google-auth` |
| JDK tools | `C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin` |

Never print, log or commit the keystore passwords or the service-account key. Unity and Play API commands spawn many processes and talk to Google, so run them with the sandbox disabled.

## 1. Pre-flight (stop and tell the user if any fail)

1. **Unity closed**: no `Unity.exe` in `tasklist` and no `Temp/UnityLockfile`. If open, ask the user to close it.
2. **Secrets present**: `C:\project\keystore.txt` (≥ 1 line) and `C:\project\play-service-account.json` (`type` = `service_account`). Check without printing contents.
3. **Git**: note the branch and `git status`. Uncommitted game changes will ship in the build, so list them and confirm with the user they belong in the release. Never commit build junk (`aab/`, `apk/`, `.utmp/`, `Assets/_Recovery/`, `Screenshots/`, `*.blend1`).
4. **Version**:
   - Current: `bundleVersion` / `AndroidBundleVersionCode` in `ProjectSettings/ProjectSettings.asset`.
   - Play: `C:\project\.playvenv\Scripts\python.exe -I Tools/upload_play.py --key C:/project/play-service-account.json --show --track <track>` prints the track and **max versionCode on any track**.
   - New versionCode = max(current, Play max) + 1.
   - New versionName = the version argument if one was given; otherwise the auto patch bump (see *Arguments*).
   - Make sure the tag `v<versionName>` doesn't already exist (`git tag -l`, `git ls-remote --tags origin`).
5. **Release notes**: draft one or two short English lines from `git log <last v-tag>..HEAD --oneline` and show them to the user with the version/track summary. **Get explicit confirmation before building/uploading.** A production track needs a separate, explicit yes.

## 2. Test

1. **Compile check**:
   `Start-Process $unity -Wait -ArgumentList "-batchmode -nographics -quit -projectPath `"$proj`" -logFile `"$proj\Logs\batch_compile.log`""`
   → fail on any `error CS` in the log.
2. **Level 7 smoke test** (no `-quit`, no `-nographics`):
   `Start-Process $unity -Wait -PassThru -ArgumentList "-batchmode -projectPath `"$proj`" -executeMethod KrakenSmokeTest.Run -logFile `"$proj\Logs\batch_smoke.log`""`
   → exit 0 = pass. On failure, show the failing lines from `Logs/kraken_smoke.log` and stop.
3. If `Assets/Tests/` exists, also run EditMode tests (`-runTests -testPlatform EditMode -testResults Logs/TestResults_EditMode.xml`, no `-quit`) and require 0 failures in the XML.

## 3. Build (run in background, ~10 min)

```powershell
$p = Start-Process $unity -Wait -PassThru -ArgumentList "-batchmode -quit -projectPath `"$proj`" -buildTarget Android -executeMethod AndroidBuild.BuildAab -versionName <name> -versionCode <code> -logFile `"$proj\Logs\batch_android.log`""; "exit $($p.ExitCode)"
```

- Success = `[AndroidBuild] SUCCESS aab\ShipWar-<name>.aab` in the log **and** the file exists with a fresh timestamp.
- **"Access is denied"** (`ld.lld.exe`, `*.o failed with output: Access is denied`, `ninja: fatal: CreateProcess`) is the Avast / Reason antivirus blocking processes. **Just re-run.** Finished work is kept, so each run gets further. Retry up to 5 runs in total. If you see `Internal build system error … backend process appears to still be running`, delete `Library/Bee/Player*` before the retry. If it still fails after 5 runs, stop and suggest antivirus exclusions for `C:\project\battle-ship` and `C:\Program Files\Unity\Hub\Editor\6000.3.7f1` (Avast **and** Reason).
- Any other error: show the `error` / `What went wrong` lines and stop.

## 4. Verify the AAB

1. `jarsigner -verify aab/ShipWar-<name>.aab` → `jar verified.`
2. `keytool -printcert -jarfile aab/ShipWar-<name>.aab` → SHA-256 equals the signing cert above.
3. With Python `zipfile`: `base/manifest/AndroidManifest.xml` contains `com.mataco.seewargame1` and `<name>`. Also check `base/lib/arm64-v8a/libil2cpp.so` is present.
4. `ProjectSettings.asset` now has the new version and **no** password text.

## 5. Upload

```
C:\project\.playvenv\Scripts\python.exe -I Tools/upload_play.py --key C:/project/play-service-account.json --aab aab/ShipWar-<name>.aab --track <track> --notes "<notes>"
```

- Expect `committed: versionCode <code> -> <track> (completed)`.
- If Google rejects `completed` because the app is still a draft, re-run with `--status draft`, then tell the user to click *Review release → Start rollout* in the Play Console.
- Permission errors right after creating the service account can take up to ~1 h to clear. Report it and stop; don't loop.

## 6. Verify on Play

Run `--show --track <track>`. The release `<name>` with code `<code>` must be listed (status `completed`, or `draft` if step 5 fell back).

## 7. Commit, tag, push

1. Stage only `ProjectSettings/ProjectSettings.asset` plus any asset files Unity re-saved during the build (review the diff). Scan the staged diff for the keystore password before committing.
2. Commit: `Release <name> (versionCode <code>) to <track>` with the attribution trailer.
3. `git tag -a v<name> -m "ShipWar <name> (versionCode <code>) — Google Play <track>"` on that commit.
4. `git push` and `git push origin v<name>`.
5. Update the "Releases so far" list under *Release tagging* in `CLAUDE.md` (commit + push).

## 8. Report

Give a short summary: version / code / track; test results (compile, smoke test, unit tests); how many build attempts were needed; AAB path + size; Play status; commit hash + tag. Remind the user that testers may need a few minutes for the update to show up.
