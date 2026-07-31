import Combine
import Foundation

@MainActor
final class CodexQuotaStore: ObservableObject {
  @Published private(set) var snapshot: QuotaSnapshot?
  @Published private(set) var isRefreshing = false
  @Published private(set) var errorMessage: String?

  private let client: CodexAppServerClient
  private var refreshTask: Task<Void, Never>?
  private var hasStarted = false

  init(client: CodexAppServerClient = CodexAppServerClient()) {
    self.client = client
  }

  func start() {
    guard !hasStarted else { return }
    hasStarted = true

    refreshTask = Task { [weak self] in
      guard let self else { return }

      await self.performRefresh()
      while !Task.isCancelled {
        do {
          try await Task.sleep(for: .seconds(60))
        } catch {
          break
        }

        guard !Task.isCancelled else { break }
        await self.performRefresh()
      }
    }
  }

  func refresh() {
    guard !isRefreshing else { return }
    Task { [weak self] in
      await self?.performRefresh()
    }
  }

  private func performRefresh() async {
    guard !isRefreshing else { return }
    isRefreshing = true
    defer { isRefreshing = false }

    do {
      snapshot = try await client.fetchSnapshot()
      errorMessage = nil
    } catch {
      errorMessage = error.localizedDescription
    }
  }

  deinit {
    refreshTask?.cancel()
  }
}
