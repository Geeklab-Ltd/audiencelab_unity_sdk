# AudienceLab Documentation

## Introduction

In the wake of Apple's ATT, mobile advertisers have lost visibility into ad performance on iOS, a challenge that will intensify with Google's upcoming privacy sandbox. Geeklab is committed to providing a privacy-centric marketing performance analytics platform that aggregates results at the device level and delivers metrics at a creative level, bypassing user-level data.

## Objectives

- Enable advertisers to run and measure the performance of iOS campaigns through Geeklab's intuitive web UI, focusing on creative-level data.

## Prerequisites

Developers and marketers will need to:

- Develop their game using Unity.
- Integrate Geeklab's AudienceLab Unity SDK.

## Integrating AudienceLab SDK into Unity

This section provides a step-by-step guide to integrate the AudienceLab SDK into your Unity project.

### Initial Setup

There are two ways to integrate the AudienceLab SDK into your Unity project:

#### Option 1: Install via Git (Recommended)

1. Open the Unity Package Manager (Window > Package Manager)
2. Click the "+" button in the top-left corner
3. Select "Add package from git URL..."
4. Enter the following URL:
   ```
   https://github.com/Geeklab-Ltd/audiencelab_unity_sdk.git
   ```
5. Click "Add"

#### Option 2: Manual Installation

1. Download the latest AudienceLab SDK package from Geeklab
2. Extract the ZIP file contents into your Unity project's `Packages` folder
3. Open/reload your Unity project to import the SDK

#### Dependencies

After installing via either method:

- Unity will automatically resolve and install required dependencies
- Verify that Newtonsoft.Json (version 3.0.2 or higher) is installed
- Check the Package Manager for any error messages
- Ensure all dependencies are properly resolved before proceeding

### Configure the SDK

1. **Open the SDK Setup**:

   - Navigate to `AudienceLab SDK` from the Unity menu to open the setup modal.
   - This user interface allows you to configure the SDK settings specific to your project.

2. **Authentication**:

   - Enter the authentication token provided by AudienceLab in the setup modal.
   - Click "Verify" to link your Unity project with your configured application on AudienceLab.
   - After verification, save the project and restart Unity to ensure the SDK token is properly saved and initialized. You can verify the token status in the SDK Settings window after restarting.

3. **Enable Features**:
   - Ensure that `isSDKEnabled` and `SendStatistics` are checked to activate the SDK's core functionality.
   - `ShowDebugLog` is optional but recommended during initial setup for troubleshooting.

### ProGuard Configuration (Android)

When building your application for Android in release mode with code obfuscation enabled:

1. **Required ProGuard Rules**: You must add the following ProGuard rule to your project to prevent obfuscation of AudienceLab SDK classes:

   ```
   -keep class com.Geeklab.plugin.** { *; }
   ```

2. **Implementation Options**:

   - Add the rule to your existing ProGuard configuration file
   - Create a new file named `proguard-user.txt` in your project's `Assets/Plugins/Android` directory with the rule above

3. **Important**: Failure to include these ProGuard rules in release builds may result in runtime errors and SDK functionality issues.

### Finalizing SDK Integration

1. **Build and Release**:

   - Once the SDK is configured, compile and build your Unity project.
   - Release the built application on the appropriate platforms.

2. **Monitor Application Performance**:
   - Utilize the AudienceLab dashboard to monitor real-time performance and analytics of your application.

## Custom Event Tracking

### SendCustomPurchaseEvent Function

This function is used to track custom purchase events within your application.

**Implementation Steps**:

1. **Call After Purchase**: Trigger this function immediately after a purchase is made.
2. **Check Configuration**: The function checks if the SDK is fully enabled.
3. **Log Event**: If logging is enabled, the purchase event is logged for debugging.
4. **Prepare and Send Data**: Data about the purchase is packaged and sent to the backend for tracking.

**Example Usage**:

```csharp
PurchaseMetrics.SendCustomPurchaseEvent("123", "Premium Pack", 0.99, "USD", "Completed");
```

```json
{
  "item_id": "string",
  "item_name": "string",
  "value": "double",
  "currency": "string",
  "status": "string"
}
```

### SendCustomAdEvent Function

This function allows for tracking ad views with detailed information about the interaction.

**Implementation Steps**:

1. **Call After Viewing Ad**: Execute this function right after an ad is viewed.
2. **Check SDK Status**: Verifies if the SDK is enabled.
3. **Log Event**: Logs the event if debugging is active.
4. **Prepare and Send Data**: Collects data about the ad view and sends it to the backend.

**Example Usage**:

```csharp
AdMetrics.SendCustomAdEvent("ad_001", "Interstitial Ad 1", "GoogleAds", 30, true, "Google", "CampaignA", 0.04, "USD");
```

```json
{
  "ad_id": "string",
  "name": "string",
  "source": "string",
  "watch_time": "int",
  "reward": "bool",
  "media_source": "string",
  "channel": "string",
  "value": "double",
  "currency": "string",
  "total_ad_value": "double"
}
```

### SendAdViewEvent Function

A simplified function for tracking ad view events with automatic cumulative ad value tracking. This function automatically accumulates and includes a `total_ad_value` field that represents the cumulative value of all ads viewed since the app was installed.

**Key Features**:
- **Automatic Accumulation**: Adds the ad value to a persistent local total each time called
- **Device-Stored**: The cumulative value is stored locally using Unity's PlayerPrefs
- **Always Included**: Every ad view event automatically includes the `total_ad_value` field
- **Cross-Session**: The accumulated value persists across app sessions and restarts

**Implementation Steps**:

1. **Call After Ad View**: Execute this function when an ad has been viewed by the user.
2. **Automatic Accumulation**: The function automatically adds the ad value to the total ad value.
3. **Persistent Storage**: The accumulated value is saved locally on the device.
4. **Include in Event**: The cumulative value is included in the event data sent to the backend.

**Example Usage**:

```csharp
// Simple ad view tracking with value
AudiencelabSDK.SendAdViewEvent("ad_12345", "unity_ads", 0.05, "USD");

// With additional details
AudiencelabSDK.SendAdViewEvent("ad_67890", "admob", 0.08, "USD", 30, true);

// Zero value ad (still tracked in total)
AudiencelabSDK.SendAdViewEvent("ad_11111", "organic_content", 0.0, "USD");
```

**Generated Event Data**:
```json
{
  "ad_id": "ad_12345",
  "name": "ad_view",
  "source": "unity_ads",
  "watch_time": 0,
  "reward": false,
  "value": 0.05,
  "currency": "USD",
  "total_ad_value": 2.47
}
```

### GetTotalAdValue Function

Retrieve the current cumulative ad value stored locally on the device.

**Example Usage**:

```csharp
double totalValue = AudiencelabSDK.GetTotalAdValue();
Debug.Log($"User has generated ${totalValue:F2} in total ad value");
```

## Conclusion

By following these steps, developers can effectively utilize AudienceLab to track and optimize their mobile advertising campaigns in a privacy-centric world. Ensure all configurations and integrations are tested thoroughly to guarantee accurate data collection and performance analysis.
