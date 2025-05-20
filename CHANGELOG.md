# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

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
