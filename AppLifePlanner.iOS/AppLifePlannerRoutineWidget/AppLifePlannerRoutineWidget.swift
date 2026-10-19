import SwiftUI
import WidgetKit

struct RoutineTimelineEntry: TimelineEntry
{
  let date: Date
  let item: RoutineWidgetItem?
  let next: RoutineWidgetItem?
  let plannedMomentCount: Int
  let completedMomentCount: Int
  let emptyTitle: String
  let emptyDetail: String
  let isConfigured: Bool
}

struct RoutineTimelineProvider: TimelineProvider
{
  private let api = AppLifePlannerWidgetApi()

  func placeholder(in context: Context) -> RoutineTimelineEntry
  {
    sampleEntry(date: Date())
  }

  func getSnapshot(
    in context: Context,
    completion: @escaping (RoutineTimelineEntry) -> Void)
  {
    completion(sampleEntry(date: Date()))
  }

  func getTimeline(
    in context: Context,
    completion: @escaping (Timeline<RoutineTimelineEntry>) -> Void)
  {
    Task
    {
      let now = Date()
      guard AppLifePlannerWidgetStore.isPaired
      else
      {
        completion(Timeline(
          entries: [unconfiguredEntry(date: now)],
          policy: .after(now.addingTimeInterval(21600))))
        return
      }

      do
      {
        let response = try await api.loadRoutineSnapshot()
        let entries = buildEntries(response: response, now: now)
        let refreshAfter = max(TimeInterval(response.refreshAfterSeconds), 21600)
        completion(Timeline(
          entries: entries,
          policy: .after(now.addingTimeInterval(refreshAfter))))
      }
      catch
      {
        completion(Timeline(
          entries: [offlineEntry(date: now)],
          policy: .after(now.addingTimeInterval(21600))))
      }
    }
  }

  private func buildEntries(
    response: LockScreenRoutineWidgetResponse,
    now: Date) -> [RoutineTimelineEntry]
  {
    let items = response.timeline.sorted { $0.startDate < $1.startDate }
    var dates = Set<Date>()
    dates.insert(now)

    for item in items where item.endDate >= now
    {
      dates.insert(max(item.startDate, now))
      dates.insert(item.endDate)

      var cursor = max(item.startDate, now)
      while cursor < item.endDate
      {
        dates.insert(cursor)
        cursor = cursor.addingTimeInterval(15 * 60)
      }
    }

    let sortedDates = dates.sorted().prefix(96)
    let entries = sortedDates.map
    {
      date in
      RoutineTimelineEntry(
        date: date,
        item: currentItem(items: items, at: date) ?? response.next,
        next: response.next,
        plannedMomentCount: response.plannedMomentCount,
        completedMomentCount: response.completedMomentCount,
        emptyTitle: response.emptyTitle,
        emptyDetail: response.emptyDetail,
        isConfigured: true)
    }

    return entries.isEmpty
      ? [RoutineTimelineEntry(
        date: now,
        item: response.current ?? response.next,
        next: response.next,
        plannedMomentCount: response.plannedMomentCount,
        completedMomentCount: response.completedMomentCount,
        emptyTitle: response.emptyTitle,
        emptyDetail: response.emptyDetail,
        isConfigured: true)]
      : Array(entries)
  }

  private func currentItem(items: [RoutineWidgetItem], at date: Date) -> RoutineWidgetItem?
  {
    items.first
    {
      item in
      item.startDate <= date && date < item.endDate
    }
  }

  private func sampleEntry(date: Date) -> RoutineTimelineEntry
  {
    RoutineTimelineEntry(
      date: date,
      item: nil,
      next: nil,
      plannedMomentCount: 15,
      completedMomentCount: 9,
      emptyTitle: String(localized: "Music"),
      emptyDetail: String(localized: "19:00-20:00 · Personal"),
      isConfigured: true)
  }

  private func unconfiguredEntry(date: Date) -> RoutineTimelineEntry
  {
    RoutineTimelineEntry(
      date: date,
      item: nil,
      next: nil,
      plannedMomentCount: 0,
      completedMomentCount: 0,
      emptyTitle: String(localized: "Pair iPhone title"),
      emptyDetail: String(localized: "Open AppLifePlanner"),
      isConfigured: false)
  }

  private func offlineEntry(date: Date) -> RoutineTimelineEntry
  {
    RoutineTimelineEntry(
      date: date,
      item: nil,
      next: nil,
      plannedMomentCount: 0,
      completedMomentCount: 0,
      emptyTitle: String(localized: "No update"),
      emptyDetail: String(localized: "We will try again today"),
      isConfigured: true)
  }
}

struct AppLifePlannerRoutineWidgetView: View
{
  @Environment(\.widgetFamily) private var family
  let entry: RoutineTimelineEntry

  var body: some View
  {
    switch family
    {
    case .accessoryCircular:
      circularView
    case .accessoryInline:
      inlineView
    default:
      rectangularView
    }
  }

  private var displayTitle: String
  {
    entry.item?.title ?? entry.emptyTitle
  }

  private var displayDetail: String
  {
    guard let item = entry.item
    else
    {
      return entry.emptyDetail
    }

    return "\(timeRange(item)) · \(item.category)"
  }

  private var progress: Double
  {
    entry.item?.progress(at: entry.date) ?? 0
  }

  private var progressPercent: Int
  {
    Int((progress * 100).rounded())
  }

  private var rectangularView: some View
  {
    VStack(alignment: .leading, spacing: 3)
    {
      Text(displayTitle)
        .font(.headline)
        .lineLimit(1)
      Text(displayDetail)
        .font(.caption2)
        .lineLimit(1)
      HStack(spacing: 8)
      {
        Text("\(entry.completedMomentCount)/\(max(entry.plannedMomentCount, 1))")
          .font(.caption2)
        Text("\(progressPercent)%")
          .font(.caption2.weight(.semibold))
        ProgressView(value: progress)
          .progressViewStyle(.linear)
      }
    }
    .containerBackground(.fill.tertiary, for: .widget)
  }

  private var circularView: some View
  {
    ZStack
    {
      ProgressView(value: progress)
        .progressViewStyle(.circular)
      Text("\(progressPercent)")
        .font(.headline.monospacedDigit())
    }
    .containerBackground(.fill.tertiary, for: .widget)
  }

  private var inlineView: some View
  {
    Text("\(displayTitle) · \(progressPercent)%")
  }

  private func timeRange(_ item: RoutineWidgetItem) -> String
  {
    "\(formatTime(item.startDate))-\(formatTime(item.endDate))"
  }

  private func formatTime(_ date: Date) -> String
  {
    let formatter = DateFormatter()
    formatter.dateFormat = "HH:mm"
    return formatter.string(from: date)
  }
}

struct AppLifePlannerRoutineWidget: Widget
{
  let kind = "AppLifePlannerRoutineWidget"

  var body: some WidgetConfiguration
  {
    StaticConfiguration(
      kind: kind,
      provider: RoutineTimelineProvider())
    {
      entry in
      AppLifePlannerRoutineWidgetView(entry: entry)
    }
    .configurationDisplayName("AppLifePlanner")
    .description("Current routine on the Lock Screen.")
    .supportedFamilies([
      .accessoryInline,
      .accessoryRectangular,
      .accessoryCircular
    ])
  }
}
