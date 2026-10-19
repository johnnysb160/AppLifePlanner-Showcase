import Foundation

enum AppLifePlannerWidgetStore
{
  static let appGroupIdentifier = "group.com.applifeplanner.widget"
  static let defaultBaseUrl = "https://applifeplanner.onrender.com"

  private static let baseUrlKey = "baseUrl"
  private static let tokenKey = "widgetToken"

  static var baseUrl: String
  {
    get
    {
      sharedDefaults.string(forKey: baseUrlKey) ?? defaultBaseUrl
    }
    set
    {
      sharedDefaults.set(newValue, forKey: baseUrlKey)
    }
  }

  static var widgetToken: String
  {
    get
    {
      sharedDefaults.string(forKey: tokenKey) ?? ""
    }
    set
    {
      sharedDefaults.set(newValue, forKey: tokenKey)
    }
  }

  static var isPaired: Bool
  {
    !widgetToken.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
  }

  static func clear()
  {
    sharedDefaults.removeObject(forKey: tokenKey)
  }

  private static var sharedDefaults: UserDefaults
  {
    UserDefaults(suiteName: appGroupIdentifier) ?? .standard
  }
}

struct AppLifePlannerWidgetApi
{
  func pair(
    baseUrl: String,
    code: String,
    deviceName: String) async throws -> PairingExchangeResponse
  {
    let endpoint = try buildUrl(baseUrl: baseUrl, path: "/api/widget/pairing/exchange")
    var request = URLRequest(url: endpoint)
    request.httpMethod = "POST"
    request.setValue("application/json", forHTTPHeaderField: "Content-Type")
    request.httpBody = try JSONEncoder().encode(PairingExchangeRequest(
      code: code,
      deviceName: deviceName))

    let (data, response) = try await URLSession.shared.data(for: request)
    try validate(response: response)
    return try JSONDecoder().decode(PairingExchangeResponse.self, from: data)
  }

  func loadRoutineSnapshot() async throws -> LockScreenRoutineWidgetResponse
  {
    let offsetMinutes = TimeZone.current.secondsFromGMT() / 60
    let language = Locale.current.language.languageCode?.identifier == "en" ? "en" : "pt"
    let query = "tzOffsetMinutes=\(offsetMinutes)&lang=\(language)"
    let endpoint = try buildUrl(
      baseUrl: AppLifePlannerWidgetStore.baseUrl,
      path: "/api/widget/routine-lock-screen",
      query: query)
    var request = URLRequest(url: endpoint)
    request.setValue(
      "Bearer \(AppLifePlannerWidgetStore.widgetToken)",
      forHTTPHeaderField: "Authorization")
    request.setValue("no-cache", forHTTPHeaderField: "Cache-Control")

    let (data, response) = try await URLSession.shared.data(for: request)
    try validate(response: response)
    return try JSONDecoder().decode(LockScreenRoutineWidgetResponse.self, from: data)
  }

  private func buildUrl(
    baseUrl: String,
    path: String,
    query: String? = nil) throws -> URL
  {
    guard var components = URLComponents(string: baseUrl.trimmingCharacters(in: .whitespacesAndNewlines))
    else
    {
      throw URLError(.badURL)
    }

    components.path = path
    components.query = query
    guard let url = components.url
    else
    {
      throw URLError(.badURL)
    }

    return url
  }

  private func validate(response: URLResponse) throws
  {
    guard let httpResponse = response as? HTTPURLResponse
    else
    {
      throw URLError(.badServerResponse)
    }

    guard (200...299).contains(httpResponse.statusCode)
    else
    {
      throw URLError(.userAuthenticationRequired)
    }
  }
}
