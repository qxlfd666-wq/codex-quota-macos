import Foundation
import Testing

@testable import CodexQuotaApp

@Suite("Codex app-server parser")
struct CodexAppServerClientTests {
  @Test("Uses the codex bucket and the strictest quota window")
  func parsesCodexBucket() throws {
    let fetchedAt = Date(timeIntervalSince1970: 1_700_000_000)
    let account: [String: Any] = [
      "account": [
        "type": "chatgpt",
        "email": "lin.test@example.com",
        "planType": "plus",
      ]
    ]
    let rateLimits: [String: Any] = [
      "rateLimits": bucket(used: 99, plan: "free"),
      "rateLimitsByLimitId": [
        "codex_other": bucket(used: 100, plan: "pro"),
        "codex": [
          "limitId": "codex",
          "planType": "pro",
          "primary": window(used: 35, minutes: 300, reset: 1_800_000_000),
          "secondary": window(used: 82, minutes: 10_080, reset: 1_900_000_000),
        ],
      ],
    ]

    let snapshot = try CodexAppServerClient.parseSnapshot(
      accountResult: account,
      rateLimitsResult: rateLimits,
      fetchedAt: fetchedAt
    )

    #expect(snapshot.displayName == "lin test")
    #expect(snapshot.email == "lin.test@example.com")
    #expect(snapshot.planName == "Codex Pro")
    #expect(snapshot.usedPercent == 82)
    #expect(snapshot.remainingPercent == 18)
    #expect(snapshot.primary?.remainingPercent == 65)
    #expect(snapshot.secondary?.remainingPercent == 18)
    #expect(snapshot.fetchedAt == fetchedAt)
  }

  @Test("Falls back to the legacy single-bucket response")
  func parsesLegacyBucket() throws {
    let rateLimits: [String: Any] = [
      "rateLimits": bucket(used: 73, plan: "pro")
    ]

    let snapshot = try CodexAppServerClient.parseSnapshot(
      accountResult: [:],
      rateLimitsResult: rateLimits
    )

    #expect(snapshot.usedPercent == 73)
    #expect(snapshot.remainingPercent == 27)
    #expect(snapshot.planName == "Codex Pro")
  }

  @Test("Rejects responses without a ChatGPT quota window")
  func rejectsMissingWindow() {
    do {
      _ = try CodexAppServerClient.parseSnapshot(
        accountResult: ["account": ["type": "apiKey"]],
        rateLimitsResult: ["rateLimits": ["limitId": "codex"]]
      )
      Issue.record("Expected an unavailable-quota error")
    } catch CodexQuotaError.quotaUnavailable {
      // Expected for API-key-only and other non-ChatGPT accounts.
    } catch {
      Issue.record("Unexpected error: \(error)")
    }
  }

  private func bucket(used: Int, plan: String) -> [String: Any] {
    [
      "limitId": "codex",
      "planType": plan,
      "primary": window(used: used, minutes: 10_080, reset: 1_800_000_000),
    ]
  }

  private func window(used: Int, minutes: Int, reset: Int) -> [String: Any] {
    [
      "usedPercent": used,
      "windowDurationMins": minutes,
      "resetsAt": reset,
    ]
  }
}
