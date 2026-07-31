import SwiftUI

@main
struct CodexQuotaApp: App {
  @NSApplicationDelegateAdaptor(CodexQuotaAppDelegate.self)
  private var appDelegate

  var body: some Scene {
    Settings {
      EmptyView()
    }
  }
}
