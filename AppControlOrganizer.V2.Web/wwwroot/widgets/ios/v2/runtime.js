/*
//-----------------------------------------------------------------------
// <copyright file="runtime.js" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------
*/

async function AppLifePlannerIosWidgetV2(bootstrap)
{
  const context = createRuntimeContext(bootstrap);
  const token = await resolveToken(context);
  const widget = new ListWidget();
  widget.refreshAfterDate = new Date(Date.now() + context.refreshAfterSeconds * 1000);

  if (!token)
  {
    renderMessage(
      widget,
      localize(context, "Parear widget", "Pair widget"),
      localize(context, "Use URL|CODIGO|pt|lock:1", "Use URL|CODE|en|lock:1"),
      "#22d3ee");
    await finishWidget(widget);
    return;
  }

  let snapshot = null;
  try
  {
    snapshot = await loadSnapshot(context, token);
  }
  catch (error)
  {
    snapshot = await loadCachedSnapshot(context);
  }

  if (!snapshot)
  {
    renderMessage(
      widget,
      localize(context, "Sem dados salvos", "No saved data"),
      localize(context, "Tentaremos novamente", "Will retry later"),
      "#64748b");
    await finishWidget(widget);
    return;
  }

  renderSnapshot(widget, context, snapshot);
  await finishWidget(widget);
}

function createRuntimeContext(bootstrap)
{
  const parameter = parseBootstrapParameter(bootstrap || {});
  return {
    baseUrl: parameter.baseUrl,
    bootstrapVersion: String((bootstrap || {}).bootstrapVersion || "2.0.0"),
    cacheSeconds: 600,
    kind: normalizeKind(parameter.kind),
    language: parameter.language,
    refreshAfterSeconds: 21600,
    visualRefreshSeconds: 300,
    requestTimeoutSeconds: runsInWidget() ? 8 : 15,
    snapshotCacheName: "AppLifePlannerIosWidgetV2Snapshot.json",
    tokenKey: "AppLifePlanner.WidgetToken",
    tokenOrCode: parameter.tokenOrCode
  };
}

function parseBootstrapParameter(bootstrap)
{
  const rawParts = String(bootstrap.rawParameter || "").split("|").map((value) => value.trim());
  const baseUrl = (rawParts[0] || bootstrap.baseUrl || "https://applifeplanner.onrender.com").replace(/\/$/, "");
  const language = rawParts[2] === "en" || bootstrap.language === "en" ? "en" : "pt";
  return {
    baseUrl: baseUrl,
    kind: rawParts[3] || bootstrap.kind || "lock:1",
    language: language,
    tokenOrCode: rawParts[1] || String(bootstrap.tokenOrCode || "")
  };
}

async function resolveToken(context)
{
  const tokenOrCode = context.tokenOrCode.trim();
  if (tokenOrCode.toLowerCase().indexOf("code:") === 0 || looksLikePairingCode(tokenOrCode))
  {
    if (runsInWidget())
    {
      return Keychain.contains(context.tokenKey) ? Keychain.get(context.tokenKey) : "";
    }

    const code = tokenOrCode.toLowerCase().indexOf("code:") === 0
      ? tokenOrCode.substring(5).trim()
      : tokenOrCode;
    if (!code)
    {
      return "";
    }

    const token = await exchangePairingCode(context, code);
    if (token)
    {
      Keychain.set(context.tokenKey, token);
    }

    return token || tokenOrCode;
  }

  if (tokenOrCode)
  {
    Keychain.set(context.tokenKey, tokenOrCode);
    return tokenOrCode;
  }

  return Keychain.contains(context.tokenKey) ? Keychain.get(context.tokenKey) : "";
}

function looksLikePairingCode(value)
{
  return /^[A-Z0-9]{3}-[A-Z0-9]{3}$/i.test(String(value || "").trim());
}

async function exchangePairingCode(context, code)
{
  const request = new Request(`${context.baseUrl}/api/widget/pairing/exchange`);
  request.method = "POST";
  request.timeoutInterval = context.requestTimeoutSeconds;
  request.headers = {
    "Content-Type": "application/json"
  };
  request.body = JSON.stringify({
    code: code,
    deviceName: "iPhone Scriptable V2"
  });
  const response = await request.loadJSON();
  return response.token || "";
}

async function loadSnapshot(context, token)
{
  const cached = await loadCachedSnapshot(context);
  if (runsInWidget() && cached && cacheIsFresh(cached) && cacheIsForToday(cached))
  {
    return selectKind(cached.data, context.kind);
  }

  if (runsInWidget() && cached)
  {
    return cacheIsForToday(cached) ? selectKind(cached.data, context.kind) : null;
  }

  if (runsInWidget())
  {
    return null;
  }

  if (context.kind.indexOf("lock:") === 0)
  {
    const routineData = await loadFastRoutine(context, token);
    const data = createSnapshotFromRoutine(context, routineData);
    await saveSnapshot(context, data);
    return selectKind(data, context.kind);
  }

  const offsetMinutes = Math.round(-new Date().getTimezoneOffset());
  const url = `${context.baseUrl}/api/widget/ios/v2/snapshot?tzOffsetMinutes=${offsetMinutes}&lang=${context.language}&kind=${encodeURIComponent(context.kind)}`;
  const request = new Request(url);
  request.timeoutInterval = context.requestTimeoutSeconds;
  request.headers = {
    Authorization: `Bearer ${token}`,
    "Cache-Control": "no-cache"
  };
  const data = await request.loadJSON();
  await saveSnapshot(context, data);
  return selectKind(data, context.kind);
}

async function loadFastRoutine(context, token)
{
  const offsetMinutes = Math.round(-new Date().getTimezoneOffset());
  const url = `${context.baseUrl}/api/widget/ios/v2/routine-fast?tzOffsetMinutes=${offsetMinutes}&lang=${context.language}`;
  const request = new Request(url);
  request.timeoutInterval = context.requestTimeoutSeconds;
  request.headers = {
    Authorization: `Bearer ${token}`,
    "Cache-Control": "no-cache"
  };
  return await request.loadJSON();
}

function createSnapshotFromRoutine(context, routineData)
{
  const widgets = createFallbackWidgets(context.language);
  const selected = findWidget(widgets, context.kind) || findWidget(widgets, "lock:1");
  return {
    schemaVersion: 2,
    runtimeVersion: "2.0.0",
    minBootstrapVersion: "2.0.0",
    generatedAtUtc: new Date().toISOString(),
    refreshAfterSeconds: routineData.refreshAfterSeconds || context.refreshAfterSeconds,
    kind: selected ? selected.kind : context.kind,
    selectedWidget: selected,
    widgets: widgets,
    emptyTitle: routineData.emptyTitle || localize(context, "Rotina livre", "Open routine"),
    emptyDetail: routineData.emptyDetail || localize(context, "Nenhum bloco ativo agora", "No active block right now"),
    today: {
      referenceDate: routineData.referenceDate,
      localNow: routineData.localNow,
      generatedAtUtc: routineData.generatedAtUtc,
      language: routineData.language || context.language,
      title: localize(context, "Hoje", "Today"),
      dateLabel: "",
      summary: `${routineData.plannedMomentCount || 0} ${localize(context, "momentos hoje", "moments today")}`,
      current: routineData.current || null,
      next: routineData.next || null,
      timeline: Array.isArray(routineData.timeline) ? routineData.timeline : [],
      focuses: [],
      actions: [],
      insights: [],
      metadata: {
        plannedMomentCount: routineData.plannedMomentCount || 0,
        completedMomentCount: routineData.completedMomentCount || 0,
        similarMomentCount: 0,
        openActionCount: 0,
        refreshAfterSeconds: routineData.refreshAfterSeconds || context.refreshAfterSeconds
      }
    }
  };
}

function createFallbackWidgets(language)
{
  const pt = language !== "en";
  return [
    createFallbackWidget("lock:1", "lock", pt ? "Rotina 1" : "Routine 1", "#22d3ee"),
    createFallbackWidget("lock:2", "lock", pt ? "Rotina 2" : "Routine 2", "#f59e0b"),
    createFallbackWidget("lock:3", "lock", pt ? "Rotina 3" : "Routine 3", "#a3e635"),
    createFallbackWidget("lock:4", "lock", pt ? "Rotina 4" : "Routine 4", "#38bdf8"),
    createFallbackWidget("lock:5", "lock", pt ? "Rotina 5" : "Routine 5", "#c084fc"),
    createFallbackWidget("lock:6", "lock", pt ? "Rotina 6" : "Routine 6", "#22d3ee"),
    createFallbackWidget("lock:7", "lock", pt ? "Rotina 7" : "Routine 7", "#f59e0b"),
    createFallbackWidget("lock:8", "lock", pt ? "Rotina 8" : "Routine 8", "#a3e635"),
    createFallbackWidget("lock:9", "lock", pt ? "Rotina 9" : "Routine 9", "#38bdf8"),
    createFallbackWidget("lock:10", "lock", pt ? "Rotina 10" : "Routine 10", "#c084fc"),
    createFallbackWidget("home:1", "home", pt ? "Rotina 1" : "Routine 1", "#22d3ee"),
    createFallbackWidget("home:2", "home", pt ? "Rotina 2" : "Routine 2", "#f59e0b"),
    createFallbackWidget("home:3", "home", pt ? "Rotina 3" : "Routine 3", "#a3e635"),
    createFallbackWidget("home:4", "home", pt ? "Rotina 4" : "Routine 4", "#38bdf8"),
    createFallbackWidget("home:5", "home", pt ? "Rotina 5" : "Routine 5", "#c084fc"),
    createFallbackWidget("home:6", "home", pt ? "Rotina 6" : "Routine 6", "#22d3ee"),
    createFallbackWidget("home:7", "home", pt ? "Rotina 7" : "Routine 7", "#f59e0b"),
    createFallbackWidget("home:8", "home", pt ? "Rotina 8" : "Routine 8", "#a3e635"),
    createFallbackWidget("home:9", "home", pt ? "Rotina 9" : "Routine 9", "#38bdf8"),
    createFallbackWidget("home:10", "home", pt ? "Rotina 10" : "Routine 10", "#c084fc")
  ];
}

function createFallbackWidget(kind, family, title, accent)
{
  return {
    kind: kind,
    family: family,
    title: title,
    detail: "",
    accent: accent,
    privacyLevel: "normal"
  };
}

async function loadCachedSnapshot(context)
{
  const fileManager = FileManager.local();
  const cachePath = fileManager.joinPath(fileManager.documentsDirectory(), context.snapshotCacheName);
  if (!fileManager.fileExists(cachePath))
  {
    return null;
  }

  try
  {
    const cached = JSON.parse(fileManager.readString(cachePath));
    if (cached.baseUrl !== context.baseUrl || cached.language !== context.language)
    {
      return null;
    }

    return cached;
  }
  catch (error)
  {
    return null;
  }
}

async function saveSnapshot(context, data)
{
  const fileManager = FileManager.local();
  const cachePath = fileManager.joinPath(fileManager.documentsDirectory(), context.snapshotCacheName);
  fileManager.writeString(cachePath, JSON.stringify({
    baseUrl: context.baseUrl,
    cachedAt: new Date().toISOString(),
    data: data,
    language: context.language
  }));
}

function cacheIsFresh(cached)
{
  const cachedAt = new Date(cached.cachedAt || 0).getTime();
  if (Number.isNaN(cachedAt))
  {
    return false;
  }

  return Date.now() - cachedAt < 10 * 60 * 1000;
}

function cacheIsForToday(cached)
{
  return snapshotIsForDate(cached ? cached.data : null, new Date());
}

function selectKind(snapshot, kind)
{
  const selected = findWidget(snapshot.widgets, kind) || findWidget(snapshot.widgets, "lock:1");
  snapshot.kind = selected ? selected.kind : kind;
  snapshot.selectedWidget = selected || snapshot.selectedWidget;
  return snapshot;
}

function renderSnapshot(widget, context, snapshot)
{
  const kind = normalizeKind(snapshot.kind || context.kind);
  const today = snapshot.today || {};
  const selected = snapshot.selectedWidget || {};
  const accent = selected.accent || "#22d3ee";
  const now = new Date();
  widget.refreshAfterDate = nextVisualRefreshDate(context, snapshot, now);

  if (!snapshotIsForDate(snapshot, now))
  {
    renderMessage(
      widget,
      localize(context, "Sem dados de hoje", "No data for today"),
      localize(context, "Atualize o widget no Scriptable", "Refresh the widget in Scriptable"),
      accent);
    return;
  }

  if (kind.indexOf("home:") === 0)
  {
    renderHomeWidget(widget, context, snapshot, kind, accent);
    return;
  }

  renderLockWidget(widget, context, snapshot, kind, accent);
}

function renderLockWidget(widget, context, snapshot, kind, accent)
{
  const family = config.widgetFamily || "accessoryRectangular";
  const today = snapshot.today || {};
  const now = new Date();
  const selection = selectTimelineItem(today, now);
  const item = selection.item;
  const next = selection.next;

  if (family === "accessoryCircular")
  {
    renderLockCircular(widget, context, today, item, next, kind, accent, now);
    return;
  }

  if (family === "accessoryInline")
  {
    widget.addText(lockInlineText(context, today, item, next, kind));
    return;
  }

  renderLockWide(widget, context, snapshot, item, next, kind, accent, now);
}

function renderLockCircular(widget, context, today, item, next, kind, accent, now)
{
  const variant = kindNumber(kind, 1);
  if (variant === 2)
  {
    widget.addImage(makeCircleBadge(shortRemaining(context, item, next, now), localize(context, "rest", "left"), accent));
    return;
  }

  if (variant === 3)
  {
    widget.addImage(makeCircleBadge(dayCount(today), localize(context, "hoje", "today"), accent));
    return;
  }

  if (variant === 4)
  {
    widget.addImage(makeCircleBadge(next ? formatTime(next.startLocal) : "--", localize(context, "prox", "next"), accent));
    return;
  }

  if (variant === 5)
  {
    widget.addImage(makeCircleBadge(String(upcomingItems(today, now, 8).length), localize(context, "fila", "queue"), accent));
    return;
  }

  const percent = item ? progressPercent(item, now) : today.metadata ? dayPercent(today.metadata) : 0;
  widget.addImage(makeRing(percent, accent));
}

function renderLockWide(widget, context, snapshot, item, next, kind, accent, now)
{
  const variant = kindNumber(kind, 6);
  if (variant === 7)
  {
    renderLockNowNext(widget, context, snapshot, item, next, accent, now);
    return;
  }

  if (variant === 8)
  {
    renderLockMiniTimeline(widget, context, snapshot.today || {}, accent, now);
    return;
  }

  if (variant === 9)
  {
    renderLockCountdown(widget, context, snapshot, item, next, accent, now);
    return;
  }

  if (variant === 10)
  {
    renderLockDayMap(widget, context, snapshot.today || {}, item, next, accent, now);
    return;
  }

  renderLockRoutineNow(widget, context, snapshot, item, next, accent, now);
}

function renderLockRoutineNow(widget, context, snapshot, item, next, accent, now)
{
  if (!item)
  {
    renderLockNext(widget, context, snapshot, next, accent);
    return;
  }

  const percent = progressPercent(item, now);
  const nextText = next ? `${localize(context, "prox.", "next")} ${next.title} ${formatTime(next.startLocal)}` : "";
  widget.setPadding(2, 4, 2, 4);
  const header = widget.addText(nextText || localize(context, "sem proximo", "no next"));
  header.font = Font.mediumSystemFont(9);
  header.textOpacity = 0.75;
  header.lineLimit = 1;

  const title = widget.addText(item.title || localize(context, "Agora", "Now"));
  title.font = Font.boldSystemFont(22);
  title.minimumScaleFactor = 0.7;
  title.lineLimit = 1;

  const detail = widget.addText(`${formatTime(item.startLocal)}-${formatTime(item.endLocal)}`);
  detail.font = Font.systemFont(12);
  detail.textOpacity = 0.85;
  detail.lineLimit = 1;

  widget.addSpacer(3);
  const footer = widget.addStack();
  footer.layoutHorizontally();
  addMetric(footer, localize(context, "RESTANTE", "LEFT"), remainingLabel(context, item, now));
  footer.addSpacer(10);
  addMetric(footer, localize(context, "RITMO", "PACE"), `${percent}%`);
  footer.addSpacer(10);
  addMetric(footer, localize(context, "HOJE", "TODAY"), dayCount(snapshot.today || {}));
}

function renderLockNext(widget, context, snapshot, item, accent)
{
  if (!item)
  {
    renderMessage(widget, snapshot.emptyTitle || localize(context, "Sem proximo", "No next"), snapshot.emptyDetail || "", accent);
    return;
  }

  renderLockMetric(
    widget,
    context,
    localize(context, "Proximo", "Next"),
    item.title,
    `${formatTime(item.startLocal)}-${formatTime(item.endLocal)} - ${item.category || item.source || ""}`,
    accent);
}

function renderLockNowNext(widget, context, snapshot, item, next, accent, now)
{
  widget.setPadding(2, 4, 2, 4);
  const row = widget.addStack();
  row.layoutHorizontally();
  addLockColumn(row, item ? localize(context, "Agora", "Now") : localize(context, "Livre", "Open"), item ? item.title : snapshot.emptyTitle, item ? remainingLabel(context, item, now) : snapshot.emptyDetail, accent);
  row.addSpacer(8);
  addLockColumn(row, localize(context, "Prox.", "Next"), next ? next.title : localize(context, "sem proximo", "no next"), next ? formatTime(next.startLocal) : "--", "#f59e0b");
}

function renderLockMiniTimeline(widget, context, today, accent, now)
{
  widget.setPadding(2, 4, 2, 4);
  const items = upcomingItems(today, now, 3);
  addTinyHeader(widget, localize(context, "Rotina", "Routine"), dayCount(today), accent);
  for (const item of items)
  {
    addTinyLine(widget, `${formatTime(item.startLocal)} ${item.title}`, item.category || item.source || "", item.colorHex || accent);
  }
}

function renderLockCountdown(widget, context, snapshot, item, next, accent, now)
{
  const target = item || next;
  if (!target)
  {
    renderMessage(widget, snapshot.emptyTitle || localize(context, "Dia livre", "Open day"), snapshot.emptyDetail || "", accent);
    return;
  }

  widget.setPadding(2, 4, 2, 4);
  addTinyHeader(widget, item ? localize(context, "Termina", "Ends") : localize(context, "Comeca", "Starts"), item ? formatTime(target.endLocal) : formatTime(target.startLocal), accent);
  const title = widget.addText(target.title);
  title.font = Font.boldSystemFont(18);
  title.minimumScaleFactor = 0.55;
  title.lineLimit = 1;
  const detail = widget.addText(item ? `${remainingLabel(context, target, now)} · ${progressPercent(target, now)}%` : `${remainingLabel(context, target, now)} · ${target.category || target.source || ""}`);
  detail.font = Font.systemFont(11);
  detail.textOpacity = 0.82;
  detail.lineLimit = 1;
}

function renderLockDayMap(widget, context, today, item, next, accent, now)
{
  widget.setPadding(2, 4, 2, 4);
  addTinyHeader(widget, localize(context, "Hoje", "Today"), dayCount(today), accent);
  const row = widget.addStack();
  row.layoutHorizontally();
  addMetric(row, localize(context, "AGORA", "NOW"), item ? `${progressPercent(item, now)}%` : "--");
  row.addSpacer(8);
  addMetric(row, localize(context, "REST", "LEFT"), item ? shortRemaining(context, item, null, now) : "--");
  row.addSpacer(8);
  addMetric(row, localize(context, "PROX", "NEXT"), next ? formatTime(next.startLocal) : "--");
}

function renderLockMetric(widget, context, label, value, detail, accent)
{
  widget.setPadding(2, 2, 2, 2);
  const labelText = widget.addText(label);
  labelText.font = Font.mediumSystemFont(9);
  labelText.textOpacity = 0.75;
  labelText.lineLimit = 1;
  const valueText = widget.addText(String(value));
  valueText.font = Font.boldSystemFont(18);
  valueText.minimumScaleFactor = 0.6;
  valueText.lineLimit = 1;
  const detailText = widget.addText(detail || "");
  detailText.font = Font.systemFont(10);
  detailText.textOpacity = 0.8;
  detailText.lineLimit = 2;
  widget.tintColor = new Color(accent);
}

function renderHomeWidget(widget, context, snapshot, kind, accent)
{
  const family = config.widgetFamily || "medium";
  const today = snapshot.today || {};
  const now = new Date();
  widget.backgroundColor = new Color("#101214");
  widget.url = `${context.baseUrl}/${homeHref(kind)}`;
  widget.setPadding(12, 12, 12, 12);

  const variant = kindNumber(kind, 1);
  if (variant === 2)
  {
    renderHomeRoutineTimeline(widget, context, today, accent, now, family, 4);
    return;
  }

  if (variant === 3)
  {
    renderHomeNowNext(widget, context, today, accent, now);
    return;
  }

  if (variant === 4)
  {
    renderHomeCountdown(widget, context, today, accent, now);
    return;
  }

  if (variant === 5)
  {
    renderHomeDayStats(widget, context, today, accent, now, family);
    return;
  }

  if (variant === 6)
  {
    renderHomeRoutineTimeline(widget, context, today, accent, now, family, 6);
    return;
  }

  if (variant === 7)
  {
    renderHomeRoutineBoard(widget, context, today, accent, now);
    return;
  }

  if (variant === 8)
  {
    renderHomeCategoryBoard(widget, context, today, accent, now);
    return;
  }

  if (variant === 9)
  {
    renderHomeProgressBoard(widget, context, today, accent, now);
    return;
  }

  if (variant === 10)
  {
    renderHomeCompactAgenda(widget, context, today, accent, now);
    return;
  }

  renderHomeRoutineToday(widget, context, today, family, accent, now);
}

function renderHomeRoutineToday(widget, context, today, family, accent, now)
{
  const selection = selectTimelineItem(today, now);
  addHomeHeader(widget, context, localize(context, "Hoje", "Today"), today.summary || "", accent);
  widget.addSpacer(6);
  if (selection.item)
  {
    addHomeTitle(widget, selection.item.title, `${formatTime(selection.item.startLocal)}-${formatTime(selection.item.endLocal)} - ${remainingLabel(context, selection.item, now)}`);
  }
  else
  {
    addHomeTitle(widget, selection.next ? selection.next.title : localize(context, "Dia livre", "Open day"), selection.next ? `${localize(context, "comeca", "starts")} ${formatTime(selection.next.startLocal)}` : localize(context, "sem bloco ativo", "no active block"));
  }

  widget.addSpacer(6);
  const row = widget.addStack();
  row.layoutHorizontally();
  addHomePill(row, localize(context, "Hoje", "Today"), dayCount(today), accent);
  row.addSpacer(6);
  addHomePill(row, localize(context, "Restante", "Left"), selection.item ? remainingLabel(context, selection.item, now) : "--", "#f59e0b");
  if (family !== "small")
  {
    row.addSpacer(6);
    addHomePill(row, localize(context, "Prox.", "Next"), selection.next ? formatTime(selection.next.startLocal) : "--", "#60a5fa");
  }
}

function renderHomeRoutineTimeline(widget, context, today, accent, now, family, count)
{
  addHomeHeader(widget, context, localize(context, "Rotina", "Routine"), today.dateLabel || today.summary || "", accent);
  widget.addSpacer(5);
  const limit = family === "small" ? Math.min(count, 3) : count;
  const items = upcomingItems(today, now, limit);
  if (items.length === 0)
  {
    addHomeTitle(widget, localize(context, "Dia livre", "Open day"), localize(context, "sem itens proximos", "no upcoming items"));
    return;
  }

  for (const item of items)
  {
    addHomeLine(widget, `${formatTime(item.startLocal)} ${item.title}`, item.category || item.source || "", item.colorHex || accent);
  }
}

function renderHomeNowNext(widget, context, today, accent, now)
{
  const selection = selectTimelineItem(today, now);
  addHomeHeader(widget, context, localize(context, "Agora / Proximo", "Now / Next"), dayCount(today), accent);
  widget.addSpacer(7);
  addHomeLine(widget, selection.item ? selection.item.title : localize(context, "Dia livre", "Open day"), selection.item ? `${remainingLabel(context, selection.item, now)} · ${progressPercent(selection.item, now)}%` : localize(context, "sem bloco ativo", "no active block"), accent);
  addHomeLine(widget, selection.next ? selection.next.title : localize(context, "sem proximo", "no next"), selection.next ? `${formatTime(selection.next.startLocal)}-${formatTime(selection.next.endLocal)}` : "", "#f59e0b");
}

function renderHomeCountdown(widget, context, today, accent, now)
{
  const selection = selectTimelineItem(today, now);
  const target = selection.item || selection.next;
  addHomeHeader(widget, context, selection.item ? localize(context, "Em andamento", "Running") : localize(context, "Proximo", "Next"), dayCount(today), accent);
  widget.addSpacer(8);
  addHomeTitle(widget, target ? shortRemaining(context, target, null, now) : "--", target ? target.title : localize(context, "Dia livre", "Open day"));
  widget.addSpacer(6);
  if (target)
  {
    addHomeLine(widget, `${formatTime(target.startLocal)}-${formatTime(target.endLocal)}`, target.category || target.source || "", target.colorHex || accent);
  }
}

function renderHomeDayStats(widget, context, today, accent, now, family)
{
  const selection = selectTimelineItem(today, now);
  addHomeHeader(widget, context, localize(context, "Resumo da rotina", "Routine summary"), today.summary || "", accent);
  widget.addSpacer(8);
  const row = widget.addStack();
  row.layoutHorizontally();
  addHomePill(row, localize(context, "Hoje", "Today"), dayCount(today), accent);
  row.addSpacer(6);
  addHomePill(row, localize(context, "Agora", "Now"), selection.item ? `${progressPercent(selection.item, now)}%` : "--", "#f59e0b");
  if (family !== "small")
  {
    row.addSpacer(6);
    addHomePill(row, localize(context, "Fila", "Queue"), String(upcomingItems(today, now, 8).length), "#60a5fa");
  }
}

function renderHomeRoutineBoard(widget, context, today, accent, now)
{
  const items = upcomingItems(today, now, 5);
  addHomeHeader(widget, context, localize(context, "Quadro da rotina", "Routine board"), dayCount(today), accent);
  widget.addSpacer(5);
  for (const item of items)
  {
    addHomeLine(widget, item.title, `${formatTime(item.startLocal)} · ${item.durationLabel || durationLabel(Math.ceil((parseDate(item.endLocal) - parseDate(item.startLocal)) / 60000))}`, item.colorHex || accent);
  }
}

function renderHomeCategoryBoard(widget, context, today, accent, now)
{
  const items = upcomingItems(today, now, 5);
  addHomeHeader(widget, context, localize(context, "Categorias", "Categories"), today.summary || "", accent);
  widget.addSpacer(5);
  for (const item of items)
  {
    addHomeLine(widget, item.category || item.source || localize(context, "Rotina", "Routine"), `${formatTime(item.startLocal)} ${item.title}`, item.colorHex || accent);
  }
}

function renderHomeProgressBoard(widget, context, today, accent, now)
{
  const selection = selectTimelineItem(today, now);
  addHomeHeader(widget, context, localize(context, "Ritmo da rotina", "Routine pace"), dayCount(today), accent);
  widget.addSpacer(7);
  if (selection.item)
  {
    addHomeTitle(widget, `${progressPercent(selection.item, now)}%`, `${selection.item.title} · ${remainingLabel(context, selection.item, now)}`);
  }
  else
  {
    addHomeTitle(widget, selection.next ? formatTime(selection.next.startLocal) : "--", selection.next ? selection.next.title : localize(context, "Dia livre", "Open day"));
  }
}

function renderHomeCompactAgenda(widget, context, today, accent, now)
{
  addHomeHeader(widget, context, localize(context, "Cronograma da rotina", "Routine timeline"), dayCount(today), accent);
  widget.addSpacer(4);
  for (const item of upcomingItems(today, now, 6))
  {
    addHomeLine(widget, `${formatTime(item.startLocal)}-${formatTime(item.endLocal)}`, item.title, item.colorHex || accent);
  }
}

function renderHomeToday(widget, context, today, family, accent)
{
  const selection = selectTimelineItem(today, new Date());
  addHomeHeader(widget, context, localize(context, "Hoje", "Today"), today.summary || "", accent);
  widget.addSpacer(6);
  if (selection.item)
  {
    addHomeTitle(widget, selection.item.title, `${formatTime(selection.item.startLocal)}-${formatTime(selection.item.endLocal)} - ${remainingLabel(context, selection.item, new Date())}`);
  }
  else
  {
    addHomeTitle(widget, localize(context, "Dia livre", "Open day"), localize(context, "sem bloco ativo", "no active block"));
  }

  widget.addSpacer(6);
  const row = widget.addStack();
  row.layoutHorizontally();
  addHomePill(row, localize(context, "Hoje", "Today"), dayCount(today), accent);
  row.addSpacer(6);
  addHomePill(row, localize(context, "Acoes", "Actions"), today.metadata ? String(today.metadata.openActionCount || 0) : "0", "#fb7185");
  if (family !== "small")
  {
    row.addSpacer(6);
    const insight = first(today.insights);
    addHomePill(row, insight ? insight.title : localize(context, "Caixa", "Cash"), insight ? insight.value : "-", "#60a5fa");
  }
}

function renderHomeTimeline(widget, context, today, kind, accent)
{
  addHomeHeader(widget, context, localize(context, "Linha do tempo", "Timeline"), today.dateLabel || "", accent);
  widget.addSpacer(5);
  const items = timelineItems(today).slice(0, 4);
  if (items.length === 0)
  {
    addHomeTitle(widget, localize(context, "Dia livre", "Open day"), localize(context, "sem itens proximos", "no upcoming items"));
    return;
  }

  for (const item of items)
  {
    addHomeLine(widget, `${formatTime(item.startLocal)} ${item.title}`, item.category || item.source || "", item.colorHex || accent);
  }
}

function renderHomeInsights(widget, context, today, kind, accent)
{
  addHomeHeader(widget, context, kind === "home:market-pulse" ? localize(context, "Mercado", "Market") : localize(context, "Financas", "Finance"), today.dateLabel || "", accent);
  widget.addSpacer(5);
  const insights = (today.insights || []).slice(kind === "home:market-pulse" ? 3 : 0, kind === "home:market-pulse" ? 7 : 4);
  if (insights.length === 0)
  {
    addHomeTitle(widget, localize(context, "Sem insight", "No insight"), "-");
    return;
  }

  for (const insight of insights)
  {
    addHomeLine(widget, `${insight.title}: ${insight.value}`, insight.detail, accent);
  }
}

function renderHomeActions(widget, context, today, accent)
{
  addHomeHeader(widget, context, localize(context, "Acoes", "Actions"), `${today.metadata ? today.metadata.openActionCount || 0 : 0} ${localize(context, "abertas", "open")}`, accent);
  widget.addSpacer(5);
  const actions = (today.actions || []).slice(0, 3);
  if (actions.length === 0)
  {
    addHomeTitle(widget, localize(context, "Tudo limpo", "All clear"), localize(context, "sem acoes abertas", "no open actions"));
    return;
  }

  for (const action of actions)
  {
    addHomeLine(widget, action.title, action.evidence || action.detail || "", accent);
  }
}

function renderHomeFocus(widget, context, today, accent)
{
  addHomeHeader(widget, context, localize(context, "Foco da semana", "Week focus"), today.dateLabel || "", accent);
  widget.addSpacer(5);
  const focuses = (today.focuses || []).slice(0, 3);
  if (focuses.length === 0)
  {
    addHomeTitle(widget, localize(context, "Definir foco", "Set focus"), localize(context, "sem foco ativo", "no active focus"));
    return;
  }

  for (const focus of focuses)
  {
    addHomeLine(widget, focus.text, focus.suggested ? localize(context, "sugerido", "suggested") : localize(context, "ativo", "active"), accent);
  }
}

function renderHomeCommandCenter(widget, context, accent)
{
  addHomeHeader(widget, context, localize(context, "Central", "Command"), context.baseUrl.replace(/^https?:\/\//, ""), accent);
  widget.addSpacer(8);
  addHomeLine(widget, localize(context, "Hoje", "Today"), "/", accent);
  addHomeLine(widget, localize(context, "Rotina", "Routine"), "/routine", "#f59e0b");
  addHomeLine(widget, localize(context, "Plano", "Plan"), "/plan", "#c084fc");
}

function renderMessage(widget, title, detail, accent)
{
  const family = config.widgetFamily || "accessoryRectangular";
  if (family === "accessoryInline")
  {
    widget.addText(title);
    return;
  }

  if (family === "accessoryCircular")
  {
    widget.addImage(makeRing(0, accent));
    return;
  }

  widget.setPadding(2, 2, 2, 2);
  const titleText = widget.addText(title);
  titleText.font = Font.boldSystemFont(16);
  titleText.minimumScaleFactor = 0.7;
  titleText.lineLimit = 1;
  const detailText = widget.addText(detail);
  detailText.font = Font.systemFont(11);
  detailText.textOpacity = 0.8;
  detailText.lineLimit = 2;
}

function addMetric(stack, label, value)
{
  const metric = stack.addStack();
  metric.layoutVertically();
  const labelText = metric.addText(label);
  labelText.font = Font.mediumSystemFont(7);
  labelText.textOpacity = 0.75;
  const valueText = metric.addText(value);
  valueText.font = Font.boldSystemFont(12);
  valueText.lineLimit = 1;
}

function addLockColumn(row, label, value, detail, accent)
{
  const column = row.addStack();
  column.layoutVertically();
  const labelText = column.addText(label);
  labelText.font = Font.mediumSystemFont(8);
  labelText.textColor = new Color(accent);
  labelText.lineLimit = 1;
  const valueText = column.addText(value || "");
  valueText.font = Font.boldSystemFont(13);
  valueText.minimumScaleFactor = 0.55;
  valueText.lineLimit = 1;
  const detailText = column.addText(detail || "");
  detailText.font = Font.systemFont(9);
  detailText.textOpacity = 0.78;
  detailText.lineLimit = 1;
}

function addTinyHeader(widget, title, detail, accent)
{
  const header = widget.addStack();
  header.layoutHorizontally();
  const titleText = header.addText(title.toUpperCase());
  titleText.font = Font.boldSystemFont(8);
  titleText.textColor = new Color(accent);
  titleText.lineLimit = 1;
  header.addSpacer();
  const detailText = header.addText(detail || "");
  detailText.font = Font.mediumSystemFont(8);
  detailText.textOpacity = 0.78;
  detailText.lineLimit = 1;
}

function addTinyLine(widget, title, detail, accent)
{
  const row = widget.addStack();
  row.layoutHorizontally();
  const marker = row.addText("•");
  marker.font = Font.boldSystemFont(10);
  marker.textColor = new Color(accent);
  row.addSpacer(3);
  const text = row.addText(title || "");
  text.font = Font.boldSystemFont(9);
  text.minimumScaleFactor = 0.55;
  text.lineLimit = 1;
  row.addSpacer(4);
  const detailText = row.addText(detail || "");
  detailText.font = Font.systemFont(8);
  detailText.textOpacity = 0.72;
  detailText.lineLimit = 1;
}

function addHomeHeader(widget, context, title, detail, accent)
{
  const header = widget.addStack();
  header.layoutHorizontally();
  const titleText = header.addText(title.toUpperCase());
  titleText.font = Font.boldSystemFont(10);
  titleText.textColor = new Color(accent);
  titleText.lineLimit = 1;
  header.addSpacer();
  const detailText = header.addText(detail || "");
  detailText.font = Font.mediumSystemFont(10);
  detailText.textColor = new Color("#a1a1aa");
  detailText.lineLimit = 1;
}

function addHomeTitle(widget, title, detail)
{
  const titleText = widget.addText(title || "");
  titleText.font = Font.boldSystemFont(22);
  titleText.textColor = Color.white();
  titleText.minimumScaleFactor = 0.6;
  titleText.lineLimit = 1;
  const detailText = widget.addText(detail || "");
  detailText.font = Font.systemFont(12);
  detailText.textColor = new Color("#d4d4d8");
  detailText.lineLimit = 1;
}

function addHomeLine(widget, title, detail, accent)
{
  const row = widget.addStack();
  row.layoutHorizontally();
  const marker = row.addText("•");
  marker.font = Font.boldSystemFont(14);
  marker.textColor = new Color(accent);
  row.addSpacer(5);
  const textStack = row.addStack();
  textStack.layoutVertically();
  const titleText = textStack.addText(title || "");
  titleText.font = Font.boldSystemFont(12);
  titleText.textColor = Color.white();
  titleText.lineLimit = 1;
  const detailText = textStack.addText(detail || "");
  detailText.font = Font.systemFont(10);
  detailText.textColor = new Color("#a1a1aa");
  detailText.lineLimit = 1;
  widget.addSpacer(4);
}

function addHomePill(row, label, value, accent)
{
  const stack = row.addStack();
  stack.layoutVertically();
  stack.backgroundColor = new Color("#27272a");
  stack.cornerRadius = 8;
  stack.setPadding(6, 8, 6, 8);
  const labelText = stack.addText(label);
  labelText.font = Font.mediumSystemFont(9);
  labelText.textColor = new Color(accent);
  labelText.lineLimit = 1;
  const valueText = stack.addText(value);
  valueText.font = Font.boldSystemFont(12);
  valueText.textColor = Color.white();
  valueText.lineLimit = 1;
}

function makeRing(percent, accent)
{
  const size = 72;
  const context = new DrawContext();
  context.size = new Size(size, size);
  context.opaque = false;
  context.respectScreenScale = true;

  const rect = new Rect(6, 6, size - 12, size - 12);
  context.setStrokeColor(new Color("#9ca3af", 0.35));
  context.setLineWidth(7);
  context.strokeEllipse(rect);

  const path = new Path();
  const center = new Point(size / 2, size / 2);
  const radius = (size - 12) / 2;
  const start = -Math.PI / 2;
  const end = start + 2 * Math.PI * Math.max(0, Math.min(percent, 100)) / 100;
  path.addArc(center, radius, start, end, false);
  context.addPath(path);
  context.setStrokeColor(new Color(accent));
  context.setLineWidth(7);
  context.strokePath();

  const text = `${percent}`;
  context.setFont(Font.boldSystemFont(21));
  context.setTextColor(Color.white());
  context.drawTextInRect(text, new Rect(0, 24, size, 28));
  return context.getImage();
}

function makeCircleBadge(value, label, accent)
{
  const size = 72;
  const context = new DrawContext();
  context.size = new Size(size, size);
  context.opaque = false;
  context.respectScreenScale = true;

  const rect = new Rect(6, 6, size - 12, size - 12);
  context.setFillColor(new Color(accent, 0.18));
  context.fillEllipse(rect);
  context.setStrokeColor(new Color(accent));
  context.setLineWidth(4);
  context.strokeEllipse(rect);
  context.setTextColor(Color.white());
  context.setFont(Font.boldSystemFont(String(value || "").length > 4 ? 16 : 20));
  context.drawTextInRect(String(value || "--"), new Rect(0, 20, size, 26));
  context.setFont(Font.mediumSystemFont(8));
  context.drawTextInRect(String(label || ""), new Rect(0, 45, size, 14));
  return context.getImage();
}

async function finishWidget(widget)
{
  Script.setWidget(widget);
  if (runsInWidget())
  {
    return;
  }

  const family = config.widgetFamily || "accessoryRectangular";
  if (family === "accessoryInline")
  {
    await widget.presentAccessoryInline();
    return;
  }

  if (family === "accessoryCircular")
  {
    await widget.presentAccessoryCircular();
    return;
  }

  if (family === "small")
  {
    await widget.presentSmall();
    return;
  }

  if (family === "large" || family === "extraLarge")
  {
    await widget.presentLarge();
    return;
  }

  if (family === "medium")
  {
    await widget.presentMedium();
    return;
  }

  await widget.presentAccessoryRectangular();
}

function selectTimelineItem(today, date)
{
  const timeline = timelineItems(today);
  const active = timeline.find((item) => isActiveAt(item, date));
  if (active)
  {
    return {
      item: active,
      next: findNext(timeline, active, date)
    };
  }

  if (today.current && hasValidRange(today.current) && isActiveAt(today.current, date))
  {
    return {
      item: today.current,
      next: today.next || findNext(timeline, today.current, date)
    };
  }

  const upcoming = timeline.find((item) => parseDate(item.startLocal) > date);
  if (upcoming)
  {
    return {
      item: null,
      next: upcoming
    };
  }

  return {
    item: null,
    next: null
  };
}

function nextVisualRefreshDate(context, snapshot, date)
{
  if (!snapshotIsForDate(snapshot, date))
  {
    return new Date(date.getTime() + (snapshot.refreshAfterSeconds || context.refreshAfterSeconds) * 1000);
  }

  const selection = selectTimelineItem(snapshot.today || {}, date);
  if (selection.item)
  {
    const nextMinute = new Date(date.getTime() + context.visualRefreshSeconds * 1000);
    const end = parseDate(selection.item.endLocal);
    return end < nextMinute ? end : nextMinute;
  }

  if (selection.next)
  {
    const nextStart = parseDate(selection.next.startLocal);
    if (nextStart > date)
    {
      return nextStart;
    }
  }

  return new Date(date.getTime() + (snapshot.refreshAfterSeconds || context.refreshAfterSeconds) * 1000);
}

function upcomingItems(today, date, count)
{
  return timelineItems(today)
    .filter((item) => parseDate(item.endLocal) > date)
    .slice(0, count);
}

function shortRemaining(context, item, next, date)
{
  const target = item || next;
  if (!target)
  {
    return "--";
  }

  const targetDate = item ? parseDate(target.endLocal) : parseDate(target.startLocal);
  const minutes = Math.max(0, Math.ceil((targetDate.getTime() - date.getTime()) / 60000));
  if (minutes >= 60)
  {
    return durationLabel(minutes);
  }

  return `${minutes}${localize(context, "m", "m")}`;
}

function snapshotIsForDate(snapshot, date)
{
  const today = snapshot ? snapshot.today || {} : {};
  const reference = String(today.referenceDate || (snapshot ? snapshot.referenceDate : "") || "").substring(0, 10);
  if (reference)
  {
    return reference === localDateKey(date);
  }

  const timeline = timelineItems(today);
  if (timeline.length === 0)
  {
    return false;
  }

  return timeline.some((item) => localDateKey(parseDate(item.startLocal)) === localDateKey(date));
}

function localDateKey(date)
{
  return [
    date.getFullYear(),
    String(date.getMonth() + 1).padStart(2, "0"),
    String(date.getDate()).padStart(2, "0")
  ].join("-");
}

function timelineItems(today)
{
  const items = Array.isArray(today.timeline) ? today.timeline : [];
  return items.filter(hasValidRange).sort(compareByStart);
}

function hasValidRange(item)
{
  return item
    && !Number.isNaN(parseDate(item.startLocal).getTime())
    && !Number.isNaN(parseDate(item.endLocal).getTime());
}

function compareByStart(left, right)
{
  return parseDate(left.startLocal).getTime() - parseDate(right.startLocal).getTime();
}

function isActiveAt(item, date)
{
  return parseDate(item.startLocal) <= date && parseDate(item.endLocal) > date;
}

function findNext(timeline, item, date)
{
  const itemEnd = parseDate(item.endLocal);
  return timeline.find((candidate) =>
    candidate !== item
    && parseDate(candidate.startLocal) >= itemEnd
    && parseDate(candidate.endLocal) > date) || null;
}

function progressPercent(item, date)
{
  const start = parseDate(item.startLocal);
  const end = parseDate(item.endLocal);
  const duration = end.getTime() - start.getTime();
  if (duration <= 0)
  {
    return 0;
  }

  return Math.round(Math.max(0, Math.min(1, (date.getTime() - start.getTime()) / duration)) * 100);
}

function dayPercent(metadata)
{
  const planned = Math.max(metadata.plannedMomentCount || 0, 1);
  return Math.round(Math.max(0, Math.min(1, (metadata.completedMomentCount || 0) / planned)) * 100);
}

function remainingLabel(context, item, date)
{
  const start = parseDate(item.startLocal);
  if (date < start)
  {
    return `${localize(context, "em", "in")} ${durationLabel(Math.ceil((start.getTime() - date.getTime()) / 60000))}`;
  }

  const end = parseDate(item.endLocal);
  return durationLabel(Math.max(0, Math.ceil((end.getTime() - date.getTime()) / 60000)));
}

function durationLabel(minutes)
{
  if (minutes >= 60)
  {
    const hours = Math.floor(minutes / 60);
    const rest = minutes % 60;
    return `${hours}h ${String(rest).padStart(2, "0")}min`;
  }

  return `${minutes} min`;
}

function formatTime(value)
{
  const date = parseDate(value);
  return `${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
}

function parseDate(value)
{
  const text = String(value || "");
  const localMatch = text.match(/^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?/);
  if (localMatch)
  {
    return new Date(
      Number(localMatch[1]),
      Number(localMatch[2]) - 1,
      Number(localMatch[3]),
      Number(localMatch[4]),
      Number(localMatch[5]),
      Number(localMatch[6] || 0));
  }

  return new Date(text);
}

function lockInlineText(context, today, item, next, kind)
{
  if (kind === "lock:agenda-inline")
  {
    if (item && next)
    {
      return `${item.title} agora - ${formatTime(next.startLocal)} ${next.title}`;
    }

    if (item)
    {
      return `${item.title} - ${progressPercent(item, new Date())}%`;
    }

    return next ? `${formatTime(next.startLocal)} ${next.title}` : localize(context, "Dia livre", "Open day");
  }

  return item
    ? `${item.title} - ${progressPercent(item, new Date())}%`
    : today.summary || localize(context, "Dia livre", "Open day");
}

function dayCount(today)
{
  return `${today.metadata ? today.metadata.completedMomentCount || 0 : 0}/${Math.max(plannedCount(today), 1)}`;
}

function plannedCount(today)
{
  return today.metadata ? today.metadata.plannedMomentCount || 0 : 0;
}

function first(values)
{
  return Array.isArray(values) && values.length > 0 ? values[0] : null;
}

function findWidget(widgets, kind)
{
  if (!Array.isArray(widgets))
  {
    return null;
  }

  return widgets.find((item) => item.kind === kind) || null;
}

function normalizeKind(kind)
{
  const value = String(kind || "lock:1").trim().toLowerCase();
  if (!value)
  {
    return "lock:1";
  }

  const aliases = {
    "lock:routine-now": "lock:6",
    "lock:routine-next": "lock:7",
    "lock:routine-ring": "lock:1",
    "lock:day-count": "lock:3",
    "lock:focus": "lock:8",
    "lock:actions": "lock:9",
    "lock:finance-pulse": "lock:10",
    "lock:agenda-inline": "lock:4",
    "home:today-medium": "home:1",
    "home:routine-timeline": "home:2",
    "home:finance-medium": "home:5",
    "home:actions": "home:7",
    "home:focus-week": "home:8",
    "home:calendar-agenda": "home:6",
    "home:market-pulse": "home:9",
    "home:command-center": "home:10"
  };
  return aliases[value] || value;
}

function kindNumber(kind, fallback)
{
  const match = String(kind || "").match(/:(\d+)$/);
  if (!match)
  {
    return fallback;
  }

  const value = Number(match[1]);
  return Number.isFinite(value) ? value : fallback;
}

function homeHref(kind)
{
  return kind.indexOf("home:") === 0 ? "routine" : "";
}

function localize(context, pt, en)
{
  return context.language === "en" ? en : pt;
}

function runsInWidget()
{
  return typeof config !== "undefined" && config.runsInWidget;
}

if (typeof module !== "undefined")
{
  module.exports = {
    cacheIsFresh,
    createRuntimeContext,
    dayCount,
    durationLabel,
    normalizeKind,
    nextVisualRefreshDate,
    parseDate,
    progressPercent,
    selectTimelineItem,
    snapshotIsForDate
  };
}
