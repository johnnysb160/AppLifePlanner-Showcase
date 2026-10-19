/*
//-----------------------------------------------------------------------
// <copyright file="AppLifePlannerRoutineLockScreen.js" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------
*/

const bootstrapVersion = "2.1.0";
const defaultBaseUrl = "https://applifeplanner.onrender.com";
const rawParameter = args.widgetParameter || "";
const baseUrl = resolveBaseUrl(rawParameter);
const runtimePath = "/widgets/ios/v2/runtime.js";
const runtimeCacheName = "AppLifePlannerIosWidgetV2Runtime.js";
const runtimeCacheInfoName = "AppLifePlannerIosWidgetV2Runtime.json";
const runtimeTimeoutSeconds = 12;

try
{
  const runtimeSource = await loadRuntime();
  const runner = new Function(
    "bootstrap",
    `${runtimeSource}\nreturn AppLifePlannerIosWidgetV2(bootstrap);`);
  await runner({
    baseUrl: baseUrl,
    bootstrapVersion: bootstrapVersion,
    rawParameter: rawParameter,
    runsInWidget: runsInWidget(),
    widgetFamily: widgetFamily()
  });
}
catch (error)
{
  const widget = new ListWidget();
  widget.refreshAfterDate = new Date(Date.now() + 60 * 60 * 1000);
  renderBootstrapFallback(
    widget,
    "AppLifePlanner",
    "Abra o Scriptable uma vez");
  Script.setWidget(widget);
  if (!runsInWidget())
  {
    await widget.presentAccessoryRectangular();
  }
}

Script.complete();

function resolveBaseUrl(parameter)
{
  const firstPart = String(parameter || "").split("|")[0].trim();
  return (firstPart || defaultBaseUrl).replace(/\/$/, "");
}

async function loadRuntime()
{
  const fileManager = FileManager.local();
  const cachePath = fileManager.joinPath(fileManager.documentsDirectory(), runtimeCacheName);
  const cacheInfoPath = fileManager.joinPath(fileManager.documentsDirectory(), runtimeCacheInfoName);
  const hasCachedRuntime = fileManager.fileExists(cachePath);
  if (runsInWidget() && hasCachedRuntime)
  {
    return fileManager.readString(cachePath);
  }

  if (runsInWidget() && !hasCachedRuntime)
  {
    throw new Error("Open this script in Scriptable once before using it as a widget.");
  }

  try
  {
    const request = new Request(`${baseUrl}${runtimePath}?bootstrap=${encodeURIComponent(bootstrapVersion)}&t=${Date.now()}`);
    request.timeoutInterval = runtimeTimeoutSeconds;
    request.headers = {
      "Cache-Control": "no-cache"
    };
    const source = await request.loadString();
    if (source.indexOf("AppLifePlannerIosWidgetV2") >= 0)
    {
      fileManager.writeString(cachePath, source);
      fileManager.writeString(cacheInfoPath, JSON.stringify({
        cachedAt: new Date().toISOString(),
        url: baseUrl
      }));
      return source;
    }
  }
  catch (error)
  {
  }

  if (fileManager.fileExists(cachePath))
  {
    return fileManager.readString(cachePath);
  }

  throw new Error("Open this script in Scriptable once before using it as a widget.");
}

function renderBootstrapFallback(widget, title, detail)
{
  widget.setPadding(2, 2, 2, 2);
  const titleText = widget.addText(title);
  titleText.font = Font.boldSystemFont(14);
  titleText.lineLimit = 1;
  const detailText = widget.addText(detail);
  detailText.font = Font.systemFont(10);
  detailText.textOpacity = 0.75;
  detailText.lineLimit = 2;
}

function runsInWidget()
{
  return typeof config !== "undefined" && config.runsInWidget;
}

function widgetFamily()
{
  return typeof config !== "undefined" ? config.widgetFamily || "" : "";
}
