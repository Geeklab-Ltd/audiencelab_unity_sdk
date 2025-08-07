#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "Models/AudiencelabModels.h"
#include "AudiencelabSDK.h"
#include "AudiencelabBlueprintLibrary.generated.h"

/**
 * Blueprint Function Library for Audiencelab SDK
 * Provides easy access to SDK functions from Blueprints
 */
UCLASS()
class AUDIENCELABSDK_API UAudiencelabBlueprintLibrary : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	// SDK Management

	/** Get the SDK instance */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK", CallInEditor = true, meta = (CallInEditor = "true"))
	static UAudiencelabSDK* GetAudiencelabSDK(const UObject* WorldContext);

	/** Check if the SDK is enabled and configured */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	static bool IsSDKEnabled(const UObject* WorldContext);

	// Authentication & Deep Links

	/** Get Creative Token if any */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	static FString GetCreativeToken(const UObject* WorldContext);

	/** Get deep link URL if any */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	static FString GetDeepLink(const UObject* WorldContext);

	// Settings

	/** Enable or disable metrics collection */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	static void ToggleMetricsCollection(const UObject* WorldContext, bool bIsEnabled);

	/** Check if metrics collection is enabled */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	static bool IsMetricsCollectionEnabled(const UObject* WorldContext);

	// Event Tracking

	/** Send custom purchase event */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK", meta = (AdvancedDisplay = "OnComplete"))
	static void SendCustomPurchaseEvent(const UObject* WorldContext, const FString& ItemId, const FString& ItemName, double Value, const FString& Currency = TEXT("USD"), const FString& Status = TEXT("Completed"), const FOnSDKOperationComplete& OnComplete = FOnSDKOperationComplete());

	/** Send custom ad event */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK", meta = (AdvancedDisplay = "OnComplete"))
	static void SendCustomAdEvent(const UObject* WorldContext, const FString& AdId, const FString& Name, const FString& Source, int32 WatchTime, bool bReward, const FString& MediaSource, const FString& Channel, double Value, const FString& Currency = TEXT("USD"), const FOnSDKOperationComplete& OnComplete = FOnSDKOperationComplete());

	/** Send simple ad view event with automatic total_ad_value tracking */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK", meta = (AdvancedDisplay = "OnComplete"))
	static void SendAdViewEvent(const UObject* WorldContext, const FString& AdId, const FString& AdSource, double Value = 0.0, const FString& Currency = TEXT("USD"), int32 WatchTime = 0, bool bReward = false, const FOnSDKOperationComplete& OnComplete = FOnSDKOperationComplete());

	/** Send user metrics with custom JSON data */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK", meta = (AdvancedDisplay = "OnComplete"))
	static void SendUserMetrics(const UObject* WorldContext, const FString& JsonData, const FOnSDKOperationComplete& OnComplete = FOnSDKOperationComplete());

	/** Send custom purchase metrics with JSON data */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK", meta = (AdvancedDisplay = "OnComplete"))
	static void SendCustomPurchaseMetrics(const UObject* WorldContext, const FString& JsonData, const FOnSDKOperationComplete& OnComplete = FOnSDKOperationComplete());

	/** Send custom ad metrics with JSON data */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK", meta = (AdvancedDisplay = "OnComplete"))
	static void SendCustomAdMetrics(const UObject* WorldContext, const FString& JsonData, const FOnSDKOperationComplete& OnComplete = FOnSDKOperationComplete());

	// Data Access

	/** Get the current cumulative total ad value stored locally */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	static double GetTotalAdValue(const UObject* WorldContext);

	// Utility Functions

	/** Convert purchase event struct to JSON string */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK", CustomThunk = true, meta = (CustomStructureParam = "PurchaseEvent"))
	static FString PurchaseEventToJson(const FAudiencelabPurchaseEvent& PurchaseEvent);
	void execPurchaseEventToJson(FFrame& Stack, RESULT_DECL);

	/** Convert ad event struct to JSON string */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK", CustomThunk = true, meta = (CustomStructureParam = "AdEvent"))
	static FString AdEventToJson(const FAudiencelabAdEvent& AdEvent);
	void execAdEventToJson(FFrame& Stack, RESULT_DECL);

private:
	/** Helper function to convert any UStruct to JSON */
	static FString StructToJson(const UStruct* StructType, const void* StructPtr);
};