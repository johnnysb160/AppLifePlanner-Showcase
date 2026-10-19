# AppLifePlanner iOS Companion

This folder contains iOS widget options for AppLifePlanner.

The free, install-now path is the Scriptable V2 bootstrap in `Scriptable/AppLifePlannerRoutineLockScreen.js`. The native SwiftUI/WidgetKit source remains available for a future signed app.

## Purpose

- Pair an iPhone with the existing `/api/widget` token flow.
- Store the widget token in Scriptable Keychain through the remote runtime.
- Render iPhone Lock Screen and Home Screen widgets from one remote V2 runtime.
- Fetch `/api/widget/ios/v2/snapshot` about four times per day when iOS grants refresh time.
- Use local calculation and cache for routine changes, progress, and offline fallback between remote refreshes.

## Free operating model

Scriptable does not require GitHub Actions, GitHub Releases, Xcode, Apple Developer Program, a new hosted service, or a paid runtime provider. Widget content is updated from the existing AppLifePlanner Web backend.

The copied Scriptable code is a stable bootstrap. It only resolves the AppLifePlanner Web URL, downloads `/widgets/ios/v2/runtime.js`, and passes the original widget parameter to that runtime. Token handling, language, widget kind, endpoint selection, layout rules, and widget variants update automatically through the remote runtime served by AppLifePlanner Web. A manual paste is only expected again if a future runtime raises `minBootstrapVersion`.

## Scriptable V2 setup

1. Install Scriptable from the App Store.
2. Create a new Scriptable script named `AppLifePlanner Routine`.
3. Paste the contents of `Scriptable/AppLifePlannerRoutineLockScreen.js`.
4. In AppLifePlanner Web Profile, create a widget pairing code.
5. In Scriptable, set the script parameter as `https://applifeplanner.onrender.com|CODIGO|pt|lock:1`.
6. Run the script once inside the Scriptable app to download the remote runtime, exchange the code, and warm the local snapshot cache.
7. Add a Scriptable Lock Screen or Home Screen widget and set its script to `AppLifePlanner Routine`.
8. After the first successful run, the script stores the widget token in Scriptable Keychain. The widget parameter can then be left as `https://applifeplanner.onrender.com||pt|lock:1`.

The fourth parameter selects the widget kind. The bootstrap does not parse or own token, language, kind, or API behavior; it keeps the raw parameter for the runtime so future changes can ship from the Web. The bootstrap stores a local runtime copy and refreshes it from the Web on normal Scriptable runs. In Lock Screen widget mode, it uses only cached runtime/data so iOS does not spend the widget time budget on network calls.

## V2 widget kinds

Lock Screen:

- `lock:1` through `lock:5`: compact routine variants intended for circular/small Lock Screen slots.
- `lock:6` through `lock:10`: wider routine variants intended for rectangular Lock Screen slots.

Home Screen:

- `home:1` through `home:5`: compact routine variants.
- `home:6` through `home:10`: denser routine variants with more timeline/detail.

The V2 runtime supports rectangular, circular, and inline Lock Screen widget families plus Scriptable small, medium, and large Home Screen widgets. It asks Scriptable/iOS for short local visual refreshes and keeps remote data refreshes long; iOS still decides when Scriptable widgets actually run. In widget mode, cached runtime/data are preferred to avoid iOS timeout limits.

## Local validation

Run from the repository root:

```powershell
node --check AppLifePlanner.iOS\Scriptable\AppLifePlannerRoutineLockScreen.js
node --check AppControlOrganizer.V2.Web\wwwroot\widgets\ios\v2\runtime.js
node AppLifePlanner.iOS\Scriptable\AppLifePlannerWidgetRuntimeHarness.js
```

## Xcode setup

Use this only for the future native app path.

1. Create a new iOS App target named `AppLifePlanner`.
2. Create a Widget Extension target named `AppLifePlannerRoutineWidget`.
3. Add the Swift files from `AppLifePlanner`, `Shared`, and `AppLifePlannerRoutineWidget` to the matching targets.
4. Enable App Groups for both targets and use the same group identifier configured in `AppLifePlannerWidgetStore.appGroupIdentifier`.
5. Run the app on the iPhone, paste a pairing code from the Web profile, then add the AppLifePlanner widget to the Lock Screen.

The first repository version is source-first because iOS projects must be signed with a local Apple team. Avoid adding a macOS GitHub Actions build unless explicitly approved.
