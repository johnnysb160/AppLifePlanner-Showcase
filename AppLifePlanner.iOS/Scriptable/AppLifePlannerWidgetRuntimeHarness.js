/*
//-----------------------------------------------------------------------
// <copyright file="AppLifePlannerWidgetRuntimeHarness.js" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------
*/

const assert = require("assert");
const fs = require("fs");
const path = require("path");
const runtime = require("../../AppControlOrganizer.V2.Web/wwwroot/widgets/ios/v2/runtime.js");
const runtimeSource = fs.readFileSync(
  path.join(__dirname, "../../AppControlOrganizer.V2.Web/wwwroot/widgets/ios/v2/runtime.js"),
  "utf8");

const kinds = [
  "lock:1",
  "lock:2",
  "lock:3",
  "lock:4",
  "lock:5",
  "lock:6",
  "lock:7",
  "lock:8",
  "lock:9",
  "lock:10",
  "home:1",
  "home:2",
  "home:3",
  "home:4",
  "home:5",
  "home:6",
  "home:7",
  "home:8",
  "home:9",
  "home:10"
];

for (const kind of kinds)
{
  const context = runtime.createRuntimeContext({
    baseUrl: "https://applifeplanner.local",
    kind: kind,
    language: "pt"
  });
  assert.strictEqual(context.kind, kind);
}

assert.strictEqual(runtime.createRuntimeContext({
  baseUrl: "https://applifeplanner.local",
  kind: "lock:routine-now",
  language: "pt"
}).kind, "lock:6");

const timeline = {
  metadata: {
    completedMomentCount: 2,
    plannedMomentCount: 13
  },
  timeline: [
    {
      category: "Trabalho",
      endLocal: "2026-08-14T10:00:00Z",
      startLocal: "2026-08-14T07:30:00Z",
      title: "Trabalho"
    },
    {
      category: "Trabalho",
      endLocal: "2026-08-14T13:00:00Z",
      startLocal: "2026-08-14T10:30:00Z",
      title: "Trabalho"
    },
    {
      category: "Pessoal",
      endLocal: "2026-08-14T14:30:00Z",
      startLocal: "2026-08-14T13:00:00Z",
      title: "Almoco"
    }
  ]
};

const active = runtime.selectTimelineItem(timeline, new Date(2026, 7, 14, 12, 47, 0));
assert.strictEqual(active.item.title, "Trabalho");
assert.strictEqual(runtime.progressPercent(active.item, new Date(2026, 7, 14, 12, 47, 0)), 91);
assert.strictEqual(active.next.title, "Almoco");
assert.strictEqual(runtime.dayCount(timeline), "2/13");

const future = runtime.selectTimelineItem(timeline, new Date(2026, 7, 14, 10, 10, 0));
assert.strictEqual(future.item, null);
assert.strictEqual(future.next.title, "Trabalho");

const done = runtime.selectTimelineItem(timeline, new Date(2026, 7, 14, 23, 0, 0));
assert.strictEqual(done.item, null);

const switched = runtime.selectTimelineItem(timeline, new Date(2026, 7, 14, 13, 15, 0));
assert.strictEqual(switched.item.title, "Almoco");
assert.strictEqual(runtime.progressPercent(switched.item, new Date(2026, 7, 14, 13, 15, 0)), 17);

const refreshContext = runtime.createRuntimeContext({
  baseUrl: "https://applifeplanner.local",
  kind: "lock:1",
  language: "pt"
});
const refreshSnapshot = createRoutineSnapshot("2026-08-14");
assert.strictEqual(
  runtime.nextVisualRefreshDate(refreshContext, refreshSnapshot, new Date(2026, 7, 14, 17, 15, 0)).getTime(),
  new Date(2026, 7, 14, 17, 20, 0).getTime());
assert.strictEqual(
  runtime.nextVisualRefreshDate(refreshContext, refreshSnapshot, new Date(2026, 7, 14, 17, 59, 30)).getTime(),
  new Date(2026, 7, 14, 18, 0, 0).getTime());
assert.strictEqual(runtime.snapshotIsForDate(refreshSnapshot, new Date(2026, 7, 15, 17, 15, 0)), false);

assert.strictEqual(runtime.durationLabel(13), "13 min");
assert.strictEqual(runtime.durationLabel(91), "1h 31min");

runFlowHarness()
  .then(() =>
  {
    console.log("AppLifePlanner iOS V2 runtime harness passed.");
  })
  .catch((error) =>
  {
    console.error(error);
    process.exitCode = 1;
  });

async function runFlowHarness()
{
  await assertFirstAppRunWithPairingCode();
  await assertAppModeRefreshesFreshCache();
  await assertWidgetModeUsesCacheWithoutNetwork();
  await assertWidgetModeRejectsOldCacheWithoutNetwork();
}

async function assertFirstAppRunWithPairingCode()
{
  const state = createScriptableState(false, null);
  await runRuntimeInScriptable(state, "https://applifeplanner.local|ABC-123|pt|lock:1");

  assert.deepStrictEqual(state.requests.map((item) => item.kind), [
    "pairing",
    "routine-fast"
  ]);
  assert.strictEqual(state.keychain.get("AppLifePlanner.WidgetToken"), "TOKEN-REAL");
  assert.strictEqual(state.requests[1].headers.Authorization, "Bearer TOKEN-REAL");
  assert.ok(state.files.get("AppLifePlannerIosWidgetV2Snapshot.json").includes("Rotina atual"));
}

async function assertWidgetModeUsesCacheWithoutNetwork()
{
  const cachedSnapshot = JSON.stringify({
    baseUrl: "https://applifeplanner.local",
    cachedAt: new Date().toISOString(),
    data: createRoutineSnapshot(localDateKey(new Date())),
    language: "pt"
  });
  const state = createScriptableState(true, cachedSnapshot);
  state.keychain.set("AppLifePlanner.WidgetToken", "TOKEN-REAL");

  await runRuntimeInScriptable(state, "https://applifeplanner.local||pt|lock:1");

  assert.deepStrictEqual(state.requests, []);
  assert.strictEqual(state.widget.setWidgetCalled, true);
  assert.ok(state.widget.instance.refreshAfterDate instanceof Date);
  assert.ok(state.widget.instance.refreshAfterDate.getTime() > Date.now());
}

async function assertAppModeRefreshesFreshCache()
{
  const cachedSnapshot = JSON.stringify({
    baseUrl: "https://applifeplanner.local",
    cachedAt: new Date().toISOString(),
    data: createRoutineSnapshot(localDateKey(new Date())),
    language: "pt"
  });
  const state = createScriptableState(false, cachedSnapshot);
  state.keychain.set("AppLifePlanner.WidgetToken", "TOKEN-REAL");

  await runRuntimeInScriptable(state, "https://applifeplanner.local||pt|lock:1");

  assert.deepStrictEqual(state.requests.map((item) => item.kind), [
    "routine-fast"
  ]);
}

async function assertWidgetModeRejectsOldCacheWithoutNetwork()
{
  const cachedSnapshot = JSON.stringify({
    baseUrl: "https://applifeplanner.local",
    cachedAt: new Date().toISOString(),
    data: createRoutineSnapshot("2026-08-14"),
    language: "pt"
  });
  const state = createScriptableState(true, cachedSnapshot);
  state.keychain.set("AppLifePlanner.WidgetToken", "TOKEN-REAL");

  await runRuntimeInScriptable(state, "https://applifeplanner.local||pt|lock:1");

  assert.deepStrictEqual(state.requests, []);
  assert.strictEqual(state.widget.setWidgetCalled, true);
  assert.ok(state.widget.texts.includes("Sem dados salvos"));
}

async function runRuntimeInScriptable(state, rawParameter)
{
  const runner = new Function(
    "bootstrap",
    "config",
    "Keychain",
    "FileManager",
    "Request",
    "ListWidget",
    "Script",
    "Font",
    "Color",
    "DrawContext",
    "Size",
    "Rect",
    "Path",
    "Point",
    `${runtimeSource}\nreturn AppLifePlannerIosWidgetV2(bootstrap);`);

  await runner(
    {
      baseUrl: "https://applifeplanner.local",
      bootstrapVersion: "2.1.0",
      rawParameter: rawParameter,
      runsInWidget: state.runsInWidget,
      widgetFamily: "accessoryRectangular"
    },
    {
      runsInWidget: state.runsInWidget,
      widgetFamily: "accessoryRectangular"
    },
    state.keychainApi,
    state.fileManagerApi,
    state.Request,
    state.ListWidget,
    state.Script,
    state.Font,
    state.Color,
    state.DrawContext,
    state.Size,
    state.Rect,
    state.Path,
    state.Point);
}

function createScriptableState(runsInWidget, cachedSnapshot)
{
  const state = {
    files: new Map(),
    keychain: new Map(),
    requests: [],
    runsInWidget: runsInWidget,
    widget: {
      instance: null,
      texts: [],
      setWidgetCalled: false
    }
  };

  if (cachedSnapshot)
  {
    state.files.set("AppLifePlannerIosWidgetV2Snapshot.json", cachedSnapshot);
  }

  state.keychainApi = {
    contains: (key) => state.keychain.has(key),
    get: (key) => state.keychain.get(key),
    set: (key, value) => state.keychain.set(key, value)
  };
  state.fileManagerApi = {
    local: () => ({
      documentsDirectory: () => "",
      fileExists: (filePath) => state.files.has(fileName(filePath)),
      joinPath: (left, right) => left ? `${left}/${right}` : right,
      readString: (filePath) => state.files.get(fileName(filePath)),
      writeString: (filePath, value) => state.files.set(fileName(filePath), value)
    })
  };
  state.Request = createRequestClass(state);
  state.ListWidget = createListWidgetClass(state);
  state.Script = {
    complete: () => {},
    setWidget: (widget) =>
    {
      state.widget.instance = widget;
      state.widget.setWidgetCalled = true;
    }
  };
  state.Font = {
    boldSystemFont: (size) => ({ size: size }),
    mediumSystemFont: (size) => ({ size: size }),
    systemFont: (size) => ({ size: size })
  };
  state.Color = function Color(value)
  {
    this.value = value;
  };
  state.Color.white = () => new state.Color("#ffffff");
  state.DrawContext = createDrawContextClass();
  state.Size = function Size(width, height)
  {
    this.width = width;
    this.height = height;
  };
  state.Rect = function Rect(x, y, width, height)
  {
    this.x = x;
    this.y = y;
    this.width = width;
    this.height = height;
  };
  state.Path = function Path()
  {
    this.addArc = () => {};
  };
  state.Point = function Point(x, y)
  {
    this.x = x;
    this.y = y;
  };

  return state;
}

function createRequestClass(state)
{
  return class Request
  {
    constructor(url)
    {
      this.url = url;
      this.headers = {};
      this.method = "GET";
      this.body = "";
      this.timeoutInterval = 0;
    }

    async loadJSON()
    {
      if (this.url.endsWith("/api/widget/pairing/exchange"))
      {
        state.requests.push({
          body: this.body,
          headers: this.headers,
          kind: "pairing",
          method: this.method,
          url: this.url
        });
        assert.strictEqual(JSON.parse(this.body).code, "ABC-123");
        return {
          token: "TOKEN-REAL"
        };
      }

      if (this.url.includes("/api/widget/ios/v2/routine-fast"))
      {
        state.requests.push({
          headers: this.headers,
          kind: "routine-fast",
          method: this.method,
          url: this.url
        });
        return createFastRoutineResponse();
      }

      throw new Error(`Unexpected request: ${this.url}`);
    }
  };
}

function createListWidgetClass(state)
{
  return class ListWidget
  {
    constructor()
    {
      this.refreshAfterDate = null;
    }

    addImage()
    {
      return {};
    }

    addSpacer()
    {
    }

    addStack()
    {
      return createStack();
    }

    addText(value)
    {
      state.widget.texts.push(String(value || ""));
      return {};
    }

    async presentAccessoryCircular()
    {
    }

    async presentAccessoryInline()
    {
    }

    async presentAccessoryRectangular()
    {
    }

    async presentLarge()
    {
    }

    async presentMedium()
    {
    }

    async presentSmall()
    {
    }

    setPadding()
    {
    }
  };
}

function createStack()
{
  return {
    addSpacer: () => {},
    addStack: () => createStack(),
    addText: () => ({}),
    layoutHorizontally: () => {},
    layoutVertically: () => {},
    setPadding: () => {}
  };
}

function createDrawContextClass()
{
  return class DrawContext
  {
    addPath()
    {
    }

    drawTextInRect()
    {
    }

    getImage()
    {
      return {};
    }

    setFont()
    {
    }

    setLineWidth()
    {
    }

    setStrokeColor()
    {
    }

    setTextColor()
    {
    }

    strokeEllipse()
    {
    }

    strokePath()
    {
    }
  };
}

function createFastRoutineResponse()
{
  return {
    current: {
      category: "Trabalho",
      endLocal: "2026-08-14T18:00:00",
      startLocal: "2026-08-14T17:00:00",
      title: "Rotina atual"
    },
    emptyDetail: "Nenhum bloco ativo agora",
    emptyTitle: "Rotina livre",
    generatedAtUtc: "2026-08-14T20:00:00Z",
    language: "pt",
    localNow: "2026-08-14T17:15:00",
    plannedMomentCount: 13,
    completedMomentCount: 5,
    referenceDate: "2026-08-14",
    refreshAfterSeconds: 21600,
    timeline: [
      {
        category: "Trabalho",
        endLocal: "2026-08-14T18:00:00",
        startLocal: "2026-08-14T17:00:00",
        title: "Rotina atual"
      }
    ]
  };
}

function createRoutineSnapshot(referenceDate)
{
  return {
    emptyDetail: "Nenhum bloco ativo agora",
    emptyTitle: "Rotina livre",
    kind: "lock:1",
    refreshAfterSeconds: 21600,
    selectedWidget: {
      accent: "#22d3ee",
      family: "lock",
      kind: "lock:1",
      title: "Rotina atual"
    },
    widgets: [
      {
        accent: "#22d3ee",
        family: "lock",
        kind: "lock:1",
        title: "Rotina atual"
      }
    ],
    today: {
      referenceDate: referenceDate,
      metadata: {
        completedMomentCount: 5,
        plannedMomentCount: 13
      },
      summary: "13 momentos hoje",
      timeline: [
        {
          category: "Trabalho",
          endLocal: `${referenceDate}T18:00:00`,
          startLocal: `${referenceDate}T17:00:00`,
          title: "Rotina atual"
        }
      ]
    }
  };
}

function fileName(filePath)
{
  return String(filePath).split(/[\\/]/).pop();
}

function localDateKey(date)
{
  return [
    date.getFullYear(),
    String(date.getMonth() + 1).padStart(2, "0"),
    String(date.getDate()).padStart(2, "0")
  ].join("-");
}
