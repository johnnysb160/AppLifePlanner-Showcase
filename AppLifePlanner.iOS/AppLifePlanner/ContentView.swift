import SwiftUI
import UIKit
import WidgetKit

struct ContentView: View
{
  @State private var baseUrl = AppLifePlannerWidgetStore.baseUrl
  @State private var pairingCode = ""
  @State private var status = AppLifePlannerWidgetStore.isPaired
    ? String(localized: "iPhone paired")
    : String(localized: "Paste the code generated in Profile")
  @State private var isPairing = false

  private let api = AppLifePlannerWidgetApi()

  var body: some View
  {
    NavigationStack
    {
      Form
      {
        Section("Server")
        {
          TextField("URL", text: $baseUrl)
            .keyboardType(.URL)
            .textInputAutocapitalization(.never)
            .autocorrectionDisabled()
        }

        Section("Pairing")
        {
          TextField("Code", text: $pairingCode)
            .textInputAutocapitalization(.characters)
            .autocorrectionDisabled()

          Button(isPairing ? String(localized: "Pairing...") : String(localized: "Pair iPhone"))
          {
            Task
            {
              await pair()
            }
          }
          .disabled(isPairing || pairingCode.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)

          Button("Remove token", role: .destructive)
          {
            AppLifePlannerWidgetStore.clear()
            WidgetCenter.shared.reloadAllTimelines()
            status = String(localized: "Token removed")
          }
        }

        Section("Widget")
        {
          Text(status)
          Text("After pairing, add AppLifePlanner to the Lock Screen.")
            .font(.footnote)
            .foregroundStyle(.secondary)
        }
      }
      .navigationTitle("AppLifePlanner")
    }
  }

  private func pair() async
  {
    isPairing = true
    defer
    {
      isPairing = false
    }

    do
    {
      let response = try await api.pair(
        baseUrl: baseUrl,
        code: pairingCode,
        deviceName: UIDevice.current.name)
      AppLifePlannerWidgetStore.baseUrl = response.baseUrl.isEmpty ? baseUrl : response.baseUrl
      AppLifePlannerWidgetStore.widgetToken = response.token
      WidgetCenter.shared.reloadAllTimelines()
      status = String(localized: "iPhone paired")
    }
    catch
    {
      status = String(localized: "Unable to pair")
    }
  }
}
