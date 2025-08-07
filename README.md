# AudienceLab Unreal Engine 5 SDK Documentation

## Introduction

In the wake of Apple's ATT, mobile advertisers have lost visibility into ad performance on iOS, a challenge that will intensify with Google's upcoming privacy sandbox. Geeklab is committed to providing a privacy-centric marketing performance analytics platform that aggregates results at the device level and delivers metrics at a creative level, bypassing user-level data.

## Objectives

- Enable advertisers to run and measure the performance of iOS campaigns through Geeklab's intuitive web UI, focusing on creative-level data.

## Prerequisites

Developers and marketers will need to:

- Develop their game using Unreal Engine 5.
- Integrate Geeklab's AudienceLab Unreal Engine 5 SDK.

## Integrating AudienceLab SDK into Unreal Engine 5

This section provides a step-by-step guide to integrate the AudienceLab SDK into your Unreal Engine 5 project.

### Initial Setup

There are two ways to integrate the AudienceLab SDK into your UE5 project:

#### Option 1: Plugin Installation (Recommended)

1. Download the latest AudienceLab UE5 SDK plugin from Geeklab
2. Extract the plugin to your project's `Plugins` folder:
   ```
   YourProject/Plugins/AudiencelabSDK/
   ```
3. Open your UE5 project
4. Go to **Edit > Plugins** 
5. Search for "Audiencelab SDK" and enable it
6. Restart Unreal Engine when prompted

#### Option 2: Engine Plugins Folder

1. Extract the plugin to your UE5 engine's plugins folder:
   ```
   [UE5_INSTALL_DIR]/Engine/Plugins/Marketplace/AudiencelabSDK/
   ```
2. The plugin will be available for all projects using this engine installation

#### Dependencies

The SDK automatically handles its dependencies:

- **HTTP Module**: For network requests
- **Json/JsonUtilities**: For data serialization  
- **Core UE5 modules**: Engine, CoreUObject, etc.
- All dependencies are automatically linked during compilation

### Configure the SDK

1. **Open Project Settings**:

   - Go to **Edit > Project Settings**
   - Navigate to **Plugins > Audiencelab SDK** in the left sidebar
   - This opens the SDK configuration panel

2. **Authentication**:

   - Enter the authentication token provided by AudienceLab in the **API Token** field
   - The token links your UE5 project with your configured application on AudienceLab
   - Save the settings - no restart required, changes take effect immediately

3. **Enable Features**:
   - Check **Enable SDK** to activate the SDK's core functionality
   - Ensure **Send Statistics** is checked to enable data collection
   - **Show Debug Log** is optional but recommended during initial setup for troubleshooting

### Android Build Configuration

When building for Android, the SDK automatically configures the necessary settings:

1. **Automatic Integration**: The SDK includes Android-specific code that integrates with UE5's build system
2. **Permissions**: Required permissions are automatically added to the Android manifest
3. **ProGuard**: If using code obfuscation, the SDK handles its own obfuscation rules

### Finalizing SDK Integration

1. **Compile the Project**:

   - Build your UE5 project for your target platforms
   - The SDK will automatically compile its C++ modules during the build process
   - No additional build steps are required

2. **Deploy and Monitor**:
   - Deploy your application to the target platforms
   - Use the AudienceLab dashboard to monitor real-time performance and analytics

## Custom Event Tracking

### SendCustomPurchaseEvent Function

This function is used to track custom purchase events within your application.

**Implementation Steps**:

1. **Call After Purchase**: Trigger this function immediately after a purchase is made.
2. **Check Configuration**: The function checks if the SDK is fully enabled.
3. **Log Event**: If logging is enabled, the purchase event is logged for debugging.
4. **Prepare and Send Data**: Data about the purchase is packaged and sent to the backend for tracking.

**Example Usage in C++**:

```cpp
// Get the SDK instance
UAudiencelabSDK* SDK = UAudiencelabSDK::Get(this);
if (SDK)
{
    SDK->SendCustomPurchaseEvent(TEXT("123"), TEXT("Premium Pack"), 0.99, TEXT("USD"), TEXT("Completed"));
}
```

**Example Usage in Blueprints**:

Use the **Send Custom Purchase Event** node from the **Audiencelab SDK** category.

**Data Structure**:
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

**Example Usage in C++**:

```cpp
// Get the SDK instance
UAudiencelabSDK* SDK = UAudiencelabSDK::Get(this);
if (SDK)
{
    SDK->SendCustomAdEvent(TEXT("ad_001"), TEXT("Interstitial Ad 1"), TEXT("GoogleAds"), 
                          30, true, TEXT("Google"), TEXT("CampaignA"), 0.04, TEXT("USD"));
}
```

**Example Usage in Blueprints**:

Use the **Send Custom Ad Event** node from the **Audiencelab SDK** category.

**Data Structure**:
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
- **Device-Stored**: The cumulative value is stored locally using UE5's config system
- **Always Included**: Every ad view event automatically includes the `total_ad_value` field
- **Cross-Session**: The accumulated value persists across app sessions and restarts

**Implementation Steps**:

1. **Call After Ad View**: Execute this function when an ad has been viewed by the user.
2. **Automatic Accumulation**: The function automatically adds the ad value to the total ad value.
3. **Persistent Storage**: The accumulated value is saved locally on the device.
4. **Include in Event**: The cumulative value is included in the event data sent to the backend.

**Example Usage in C++**:

```cpp
// Get the SDK instance
UAudiencelabSDK* SDK = UAudiencelabSDK::Get(this);
if (SDK)
{
    // Simple ad view tracking with value
    SDK->SendAdViewEvent(TEXT("ad_12345"), TEXT("unity_ads"), 0.05, TEXT("USD"));

    // With additional details
    SDK->SendAdViewEvent(TEXT("ad_67890"), TEXT("admob"), 0.08, TEXT("USD"), 30, true);

    // Zero value ad (still tracked in total)
    SDK->SendAdViewEvent(TEXT("ad_11111"), TEXT("organic_content"), 0.0, TEXT("USD"));
}
```

**Example Usage in Blueprints**:

Use the **Send Ad View Event** node from the **Audiencelab SDK** category.

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

**Example Usage in C++**:

```cpp
UAudiencelabSDK* SDK = UAudiencelabSDK::Get(this);
if (SDK)
{
    double TotalValue = SDK->GetTotalAdValue();
    UE_LOG(LogTemp, Log, TEXT("User has generated $%.2f in total ad value"), TotalValue);
}
```

**Example Usage in Blueprints**:

Use the **Get Total Ad Value** node from the **Audiencelab SDK** category.

## Conclusion

By following these steps, developers can effectively integrate AudienceLab into their Unreal Engine 5 projects to track and optimize mobile advertising campaigns in a privacy-centric world. The SDK provides both C++ and Blueprint interfaces, making it accessible to all types of UE5 developers.

### Key Benefits:
- **Native UE5 Integration**: Built specifically for Unreal Engine 5 with full C++ and Blueprint support
- **Cross-Platform**: Works on Windows, Mac, Linux, Android, and iOS
- **Privacy-Focused**: Aggregates data at device level while protecting user privacy
- **Easy Configuration**: Simple setup through UE5's Project Settings
- **Automatic Value Tracking**: Built-in cumulative ad value tracking with persistent storage

Ensure all configurations and integrations are tested thoroughly to guarantee accurate data collection and performance analysis.
