# AudienceLab Documentation

## Introduction

In the wake of Apple's ATT, mobile advertisers have lost visibility into ad performance on iOS, a challenge that will intensify with Google’s upcoming privacy sandbox. Geeklab is committed to providing a privacy-centric marketing performance analytics platform that aggregates results at the device level and delivers metrics at a creative level, bypassing user-level data.

## Objectives

- Enable advertisers to run and measure the performance of iOS campaigns through Geeklab’s intuitive web UI, focusing on creative-level data.

## Prerequisites

Developers and marketers will need to:

- Develop their game using Unity.
- Integrate Geeklab’s AudienceLab Unity SDK.

## Integrating AudienceLab SDK into Unity

This section provides a step-by-step guide to integrate the AudienceLab SDK into your Unity project.

### Initial Setup

1. **Download the SDK**: Obtain the latest version of the AudienceLab SDK ZIP file provided by Geeklab.

2. **Import the SDK into Unity**:

   - Unzip the downloaded SDK into your Unity project's `Packages` folder.
   - Open your Unity project and ensure that the SDK contents are correctly imported.

3. **Resolve Dependencies**:
   - Unity's dependency resolver will automatically start upon opening the project after the SDK has been added.
   - Confirm that all dependencies are correctly installed without errors.

### Configure the SDK

1. **Open the SDK Setup**:

   - Navigate to `AudienceLab SDK` from the Unity menu to open the setup modal.
   - This user interface allows you to configure the SDK settings specific to your project.

2. **Authentication**:

   - Enter the authentication token provided by AudienceLab in the setup modal.
   - Click "Verify" to link your Unity project with your configured application on AudienceLab.

3. **Enable Features**:
   - Ensure that `isSDKEnabled` and `SendStatistics` are checked to activate the SDK’s core functionality.
   - `ShowDebugLog` is optional but recommended during initial setup for troubleshooting.

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
SendCustomPurchaseEvent("123", "Premium Pack", 299, "USD", "Completed");
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
SendCustomAdEvent("ad_001", "Interstitial Ad 1", "GoogleAds", 30, true, "Google", "CampaignA");
```

## Conclusion

By following these steps, developers can effectively utilize AudienceLab to track and optimize their mobile advertising campaigns in a privacy-centric world. Ensure all configurations and integrations are tested thoroughly to guarantee accurate data collection and performance analysis.
