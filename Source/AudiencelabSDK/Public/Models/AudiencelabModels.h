#pragma once

#include "CoreMinimal.h"
#include "AudiencelabModels.generated.h"

/**
 * API Endpoints for Audiencelab services
 */
UCLASS(BlueprintType)
class AUDIENCELABSDK_API UAudiencelabApiEndpoints : public UObject
{
	GENERATED_BODY()

public:
	static const FString API_ENDPOINT;
	static const FString TEST_TOKEN;
	static const FString CHECK_DATA_COLLECTION_STATUS;
	static const FString VERIFY_API_KEY;
	static const FString VERIFY_TOKEN;
	static const FString DEVICE_METRICS;
	static const FString FETCH_TOKEN;
	static const FString WEBHOOK;
};

/**
 * Device Information Model
 */
USTRUCT(BlueprintType)
struct AUDIENCELABSDK_API FAudiencelabDeviceInfo
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Device Info")
	float Dpi = 0.0f;

	UPROPERTY(BlueprintReadWrite, Category = "Device Info")
	int32 Width = 0;

	UPROPERTY(BlueprintReadWrite, Category = "Device Info")
	int32 Height = 0;

	UPROPERTY(BlueprintReadWrite, Category = "Device Info")
	int32 NativeWidth = 0;

	UPROPERTY(BlueprintReadWrite, Category = "Device Info")
	int32 NativeHeight = 0;

	UPROPERTY(BlueprintReadWrite, Category = "Device Info")
	bool bLowPower = false;

	UPROPERTY(BlueprintReadWrite, Category = "Device Info")
	FString Timezone;

	UPROPERTY(BlueprintReadWrite, Category = "Device Info")
	FString OsVersion;

	UPROPERTY(BlueprintReadWrite, Category = "Device Info")
	FString DeviceName;

	UPROPERTY(BlueprintReadWrite, Category = "Device Info")
	FString GraphicsDeviceVendor;

	UPROPERTY(BlueprintReadWrite, Category = "Device Info")
	TArray<FString> InstalledFonts;

	UPROPERTY(BlueprintReadWrite, Category = "Device Info")
	FString DeviceModel;

	UPROPERTY(BlueprintReadWrite, Category = "Device Info")
	FString GraphicsDeviceID;

	UPROPERTY(BlueprintReadWrite, Category = "Device Info")
	FString GraphicsDeviceVersion;
};

/**
 * Ad Event Data
 */
USTRUCT(BlueprintType)
struct AUDIENCELABSDK_API FAudiencelabAdEvent
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Ad Event")
	FString AdId;

	UPROPERTY(BlueprintReadWrite, Category = "Ad Event")
	FString Name;

	UPROPERTY(BlueprintReadWrite, Category = "Ad Event")
	FString Source;

	UPROPERTY(BlueprintReadWrite, Category = "Ad Event")
	int32 WatchTime = 0;

	UPROPERTY(BlueprintReadWrite, Category = "Ad Event")
	bool bReward = false;

	UPROPERTY(BlueprintReadWrite, Category = "Ad Event")
	FString MediaSource;

	UPROPERTY(BlueprintReadWrite, Category = "Ad Event")
	FString Channel;

	UPROPERTY(BlueprintReadWrite, Category = "Ad Event")
	double Value = 0.0;

	UPROPERTY(BlueprintReadWrite, Category = "Ad Event")
	FString Currency = TEXT("USD");

	UPROPERTY(BlueprintReadWrite, Category = "Ad Event")
	double TotalAdValue = 0.0;
};

/**
 * Purchase Event Data
 */
USTRUCT(BlueprintType)
struct AUDIENCELABSDK_API FAudiencelabPurchaseEvent
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Purchase Event")
	FString ItemId;

	UPROPERTY(BlueprintReadWrite, Category = "Purchase Event")
	FString ItemName;

	UPROPERTY(BlueprintReadWrite, Category = "Purchase Event")
	double Value = 0.0;

	UPROPERTY(BlueprintReadWrite, Category = "Purchase Event")
	FString Currency = TEXT("USD");

	UPROPERTY(BlueprintReadWrite, Category = "Purchase Event")
	FString Status;
};

/**
 * Token verification request
 */
USTRUCT(BlueprintType)
struct AUDIENCELABSDK_API FAudiencelabTokenRequest
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Token")
	FString Token;
};

/**
 * Webhook request data structure
 */
USTRUCT(BlueprintType)
struct AUDIENCELABSDK_API FAudiencelabWebhookData
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Webhook")
	FString Type;

	UPROPERTY(BlueprintReadWrite, Category = "Webhook")
	FString CreatedAt;

	UPROPERTY(BlueprintReadWrite, Category = "Webhook")
	FString CreativeToken;

	UPROPERTY(BlueprintReadWrite, Category = "Webhook")
	FString DeviceName;

	UPROPERTY(BlueprintReadWrite, Category = "Webhook")
	FString DeviceModel;

	UPROPERTY(BlueprintReadWrite, Category = "Webhook")
	FString OsSystem;

	UPROPERTY(BlueprintReadWrite, Category = "Webhook")
	FString UtcOffset;

	UPROPERTY(BlueprintReadWrite, Category = "Webhook")
	FString RetentionDay;

	// Payload will be serialized separately as JSON
};