# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.1.3] - 2026-02-19

### Added

- EDM (External Dependency Manager) support: the SDK now generates an `AudienceLabIdentityDependencies.xml` file alongside the Gradle file, so projects using EDM / Play Services Resolver can resolve Play Services dependencies automatically
- Both dependency mechanisms (Gradle file + EDM XML) are generated and managed together based on SDK settings

### Changed

- Refactored `AudienceLabIdentity.java` to use reflection for Play Services classes instead of direct imports — the file now compiles regardless of whether Play Services dependencies are present
- Android identity settings changes now immediately regenerate dependency files (instead of waiting until build time), which is important for EDM users since EDM resolves dependencies in the editor
- Updated dependency validation in SDK settings to detect both Gradle and EDM XML dependency files
- Updated setup instructions to document EDM support

## [1.1.2] - 2026-02-06

### Fixed (hotfix)

- Removed SDK test assemblies from the package. The tests ran in Edit Mode while the code under test (e.g. `WebRequestManager.Instance`) uses `DontDestroyOnLoad`, which only works in Play Mode, causing integration errors. Tests and `testables` have been removed from the distributed package.

## [1.1.1] - 2026-01-27

### Added

- Android build preprocessor that automatically manages Play Services dependencies based on SDK settings
- New menu items: "Audiencelab SDK > Regenerate Android Dependencies" and "Check Android Dependencies"
- Conditional dependency inclusion: Play Services dependencies only added when Auto GAID or App Set ID auto-collection is enabled
- `dev` flag on all events to distinguish development/debug builds from production (true when Editor or Development Build)

### Changed

- Android dependencies are now generated at build time instead of being bundled with the SDK
- Updated Editor validation to reflect automatic dependency generation
- Improved setup instructions in SDK settings window
- Merged "Manual GAID" and "Disabled" modes into single "Disabled" option (manual SetAdvertisingId() works regardless of mode)
- App Set ID auto-collection checkbox now always editable to allow disabling Play Services dependencies

### Fixed

- Removed unnecessary BILLING permission from AndroidManifest.xml

## [1.1.0] - 2026-01-22

### Added

- Event de-duplication fields: `event_id` and optional `dedupe_key` on all webhook requests
- Identity collection: iOS IDFV, Android GAID/App Set ID (+ optional Android ID fallback)
- Session tracking with `session` events and session identifiers
- User properties (whitelisted/blacklisted) persisted in PlayerPrefs and attached to all requests
- Public `AudiencelabSDK.SendCustomEvent` API for custom events
- Identity settle window (2s) for creative token fetching and session start events when GAID is enabled
- Optional Debug Overlay (Editor/Development only) for request status and recent events
- Token fetch retry loop with exponential backoff (max 10 retries, 5 minute cap)
- Webhook event queue with file persistence for offline or missing-token scenarios
- Automatic queue flush when token becomes available or connectivity is restored
- `AudiencelabSDK.SendAdEvent()` and `AudiencelabSDK.SendPurchaseEvent()` convenience methods
- Reserved property keys (starting with `_`) for backend-only use; developers cannot set or unset these
- Enhanced debug overlay with token status, last fetch attempt time, and request envelope snapshot
- Connectivity monitoring to reset retry counts and flush queued events when back online

### Changed

- Fetch-token request now includes identity and user properties
- Token response can return whitelisted properties which are merged locally
- `retention_day` sent as `int?` instead of string in webhook payloads
- Editor settings UI reorganized into tabs (Main, Privacy, Debug)
- Deprecated direct `AdMetrics` and `PurchaseMetrics` method calls in favor of `AudiencelabSDK` methods

### Fixed

- Fixed iOS build errors caused by `DllImport` declarations incorrectly placed inside method bodies
- Fixed Android/iOS native code attempting to compile in Editor by adding `!UNITY_EDITOR` guards
- Removed unused Unity Ads and Unity Purchasing assembly references that could cause build issues

## [1.0.1] - 2025-08-25

### Added

- Added optional `tr_id` (transaction ID) field to purchase events for better transaction tracking and deduplication
- The `tr_id` field is now available in both `SendCustomPurchaseEvent` and `SendPurchaseEvent` methods

### Fixed

- Fixed floating-point precision errors in cumulative value tracking for both purchase and ad events
- Changed storage mechanism from PlayerPrefs.SetFloat/GetFloat to PlayerPrefs.SetString/GetString to maintain full double precision
- This resolves issues where cumulative values were accumulating rounding errors over multiple events
- Both `total_purchase_value` and `total_ad_value` now maintain accurate precision across all events
- Only cumulate total purchase value when the status is completed or success
- Enhanced number parsing and formatting with culture-invariant culture and robust number styles for international compatibility

## [1.0.0] - 2025-08-XX

### Added

- Introduced cumulative `total_purchase_value` tracking for purchase events. The SDK now maintains a persistent, device-local sum of all purchase values (revenue) generated by the user.
- Every purchase event now automatically includes a `total_purchase_value` field, representing the total value of all purchases made by the user since app install.
- The cumulative value is stored using Unity's PlayerPrefs and persists across app sessions and device restarts.
- Updated `SendCustomPurchaseEvent` and new `SendPurchaseEvent` APIs to automatically update and include `total_purchase_value` in event payloads.
- Added `AudiencelabSDK.GetTotalPurchaseValue()` to retrieve the current cumulative purchase value for the user.
- Updated documentation and usage examples to reflect the new purchase API and event data structure.
- Added SDK version tracking to all web requests. Every request now includes `sdk_version` and `sdk_type` fields for better analytics and debugging.
- Created centralized `SDKVersion` class to manage SDK version information across the entire SDK.
- All webhook requests and device metrics requests now include SDK version information in their payloads.
- Added dynamic app version tracking using Unity's `Application.version` and `Application.bundleVersion`.
- All web requests now include `app_version` and `app_bundle_version` fields for comprehensive version tracking.
- Added public methods `GetAppVersion()`, `GetAppBundleVersion()`, `GetSDKVersion()`, and `GetUnityVersion()` for easy access to version information.

## [0.9.9] - 2025-07-22

### Added

- Introduced cumulative `total_ad_value` tracking for ad events. The SDK now maintains a persistent, device-local sum of all ad values (revenue) generated by the user.
- Every ad event now automatically includes a `total_ad_value` field, representing the total value of all ads viewed by the user since app install.
- The cumulative value is stored using Unity's PlayerPrefs and persists across app sessions and device restarts.
- Updated `SendCustomAdEvent` and new `SendAdViewEvent` APIs to automatically update and include `total_ad_value` in event payloads.
- Added `AudiencelabSDK.GetTotalAdValue()` to retrieve the current cumulative ad value for the user.
- Updated documentation and usage examples to reflect the new API and event data structure.

## [0.9.61] - 2025-05-20

### Fixed

- Fixed NullReferenceException in TokenHandler.SetToken when network requests fail
- Added null checks to prevent crashes when token fetching fails
- Improved error handling in token acquisition process

## [0.9.6] - 2025-03-27

### Fixed

- Fixed empty request body issue on Unity 6.x for iOS platform
- Improved request serialization consistency across all platforms
- Added proper serializable classes for all network requests
- Fixed null reference exceptions in Unity Editor for device info
- Standardized JSON serialization approach across the SDK

### Changed

- Refactored request models into dedicated classes for better type safety
- Improved platform-specific handling for device information
- Enhanced error handling for network requests

### Added

- Added new serializable classes for system info and GPU content
- Added better support for Unity Editor testing

## [0.9.53] - 2025-03-10

### Minor updates

- Added automatic creation of Resources directory if missing during SDK installation
- Implemented package identifier and ProGuard configuration for Android builds to prevent code obfuscation issues
- Encapsulated JsonConverter within SDK namespace to avoid conflicts with other packages

## [0.9.52] - 2024-11-28

### Hot Fix

- Removed the un-necessary Android specific resolution scaling that could cause build errors.

## [0.9.51] - 2024-11-27

### Changed

- Added support for Resolution Scaling
- Added UTC offset and retention day tracking to payload for more granular creative-level ROAS metrics and improved UTC-based analytics
- Some minor clean up

## [0.9.5] - 2024-10-29

### Changed

- Modified the ad payload to include revenue and currency
- Modified the value data types to [double] from [int]
- Added the data types to #readme tutorial

## [0.9.1] - 2024-09-26

### Changed

- Removed Unity Ads and Unity Purchases dependencies
- Cleaned up SDK settings
- Overall code cleanup and optimization

### Removed

- Unity Ads integration
- Unity Purchases integration

### Improved

- Simplified SDK configuration process
- Enhanced performance by removing unused dependencies

## [0.9.4] - 2024-10-07

### Changed

- Cleaned up the codebase.
- Optimized the SDK for performance.
- Improved the documentation.

## [0.9.0] - 2024-07-24

#### Initial release
