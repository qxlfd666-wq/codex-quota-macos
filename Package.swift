// swift-tools-version: 6.0

import PackageDescription

let package = Package(
  name: "CodexQuota",
  platforms: [
    .macOS(.v14)
  ],
  products: [
    .executable(name: "CodexQuota", targets: ["CodexQuotaApp"])
  ],
  targets: [
    .executableTarget(
      name: "CodexQuotaApp",
      path: "Sources/CodexQuotaApp"
    ),
    .testTarget(
      name: "CodexQuotaAppTests",
      dependencies: ["CodexQuotaApp"],
      path: "Tests/CodexQuotaAppTests"
    ),
  ]
)
