import Foundation

struct PairingExchangeRequest: Encodable
{
  let code: String
  let deviceName: String
}

struct PairingExchangeResponse: Decodable
{
  let token: String
  let baseUrl: String
}

struct LockScreenRoutineWidgetResponse: Decodable
{
  let referenceDate: String
  let localNow: String
  let generatedAtUtc: String
  let language: String
  let current: RoutineWidgetItem?
  let next: RoutineWidgetItem?
  let timeline: [RoutineWidgetItem]
  let plannedMomentCount: Int
  let completedMomentCount: Int
  let refreshAfterSeconds: Int
  let emptyTitle: String
  let emptyDetail: String
}

struct RoutineWidgetItem: Decodable, Identifiable
{
  let id: Int
  let title: String
  let source: String
  let category: String
  let startLocal: String
  let endLocal: String
  let href: String
  let iconCss: String
  let colorHex: String
  let isCurrent: Bool
  let isCalendar: Bool
  let progressPercent: Int
  let durationLabel: String
  let remainingLabel: String
  let nextLabel: String
}

extension RoutineWidgetItem
{
  var startDate: Date
  {
    AppLifePlannerDateParser.parseLocal(startLocal) ?? Date()
  }

  var endDate: Date
  {
    AppLifePlannerDateParser.parseLocal(endLocal) ?? startDate.addingTimeInterval(60)
  }

  func progress(at date: Date) -> Double
  {
    let duration = endDate.timeIntervalSince(startDate)
    if duration <= 0
    {
      return 0
    }

    let elapsed = date.timeIntervalSince(startDate)
    return min(max(elapsed / duration, 0), 1)
  }
}

enum AppLifePlannerDateParser
{
  private static let localFormatters: [DateFormatter] =
  {
    let formats = [
      "yyyy-MM-dd'T'HH:mm:ss.SSSSSSS",
      "yyyy-MM-dd'T'HH:mm:ss.SSS",
      "yyyy-MM-dd'T'HH:mm:ss"
    ]
    return formats.map
    {
      format in
      let formatter = DateFormatter()
      formatter.calendar = Calendar(identifier: .gregorian)
      formatter.locale = Locale(identifier: "en_US_POSIX")
      formatter.dateFormat = format
      return formatter
    }
  }()

  static func parseLocal(_ value: String) -> Date?
  {
    for formatter in localFormatters
    {
      if let date = formatter.date(from: value)
      {
        return date
      }
    }

    return ISO8601DateFormatter().date(from: value)
  }
}
