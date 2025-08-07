#include "AudiencelabBlueprintLibrary.h"
#include "AudiencelabSDKModule.h"
#include "AudiencelabSettings.h"
#include "Json.h"
#include "JsonObjectConverter.h"

UAudiencelabSDK* UAudiencelabBlueprintLibrary::GetAudiencelabSDK(const UObject* WorldContext)
{
	return UAudiencelabSDK::Get(WorldContext);
}

bool UAudiencelabBlueprintLibrary::IsSDKEnabled(const UObject* WorldContext)
{
	UAudiencelabSDK* SDK = GetAudiencelabSDK(WorldContext);
	if (SDK)
	{
		return SDK->GetIsMetricsCollection();
	}
	
	const UAudiencelabSettings* Settings = UAudiencelabSettings::Get();
	return Settings ? Settings->IsFullyEnabled() : false;
}

FString UAudiencelabBlueprintLibrary::GetCreativeToken(const UObject* WorldContext)
{
	UAudiencelabSDK* SDK = GetAudiencelabSDK(WorldContext);
	return SDK ? SDK->GetCreativeToken() : FString();
}

FString UAudiencelabBlueprintLibrary::GetDeepLink(const UObject* WorldContext)
{
	UAudiencelabSDK* SDK = GetAudiencelabSDK(WorldContext);
	return SDK ? SDK->GetDeepLink() : FString();
}

void UAudiencelabBlueprintLibrary::ToggleMetricsCollection(const UObject* WorldContext, bool bIsEnabled)
{
	UAudiencelabSDK* SDK = GetAudiencelabSDK(WorldContext);
	if (SDK)
	{
		SDK->ToggleMetricsCollection(bIsEnabled);
	}
}

bool UAudiencelabBlueprintLibrary::IsMetricsCollectionEnabled(const UObject* WorldContext)
{
	UAudiencelabSDK* SDK = GetAudiencelabSDK(WorldContext);
	return SDK ? SDK->GetIsMetricsCollection() : false;
}

void UAudiencelabBlueprintLibrary::SendCustomPurchaseEvent(const UObject* WorldContext, const FString& ItemId, const FString& ItemName, double Value, const FString& Currency, const FString& Status, const FOnSDKOperationComplete& OnComplete)
{
	UAudiencelabSDK* SDK = GetAudiencelabSDK(WorldContext);
	if (SDK)
	{
		SDK->SendCustomPurchaseEvent(ItemId, ItemName, Value, Currency, Status, OnComplete);
	}
	else
	{
		OnComplete.ExecuteIfBound(false);
	}
}

void UAudiencelabBlueprintLibrary::SendCustomAdEvent(const UObject* WorldContext, const FString& AdId, const FString& Name, const FString& Source, int32 WatchTime, bool bReward, const FString& MediaSource, const FString& Channel, double Value, const FString& Currency, const FOnSDKOperationComplete& OnComplete)
{
	UAudiencelabSDK* SDK = GetAudiencelabSDK(WorldContext);
	if (SDK)
	{
		SDK->SendCustomAdEvent(AdId, Name, Source, WatchTime, bReward, MediaSource, Channel, Value, Currency, OnComplete);
	}
	else
	{
		OnComplete.ExecuteIfBound(false);
	}
}

void UAudiencelabBlueprintLibrary::SendAdViewEvent(const UObject* WorldContext, const FString& AdId, const FString& AdSource, double Value, const FString& Currency, int32 WatchTime, bool bReward, const FOnSDKOperationComplete& OnComplete)
{
	UAudiencelabSDK* SDK = GetAudiencelabSDK(WorldContext);
	if (SDK)
	{
		SDK->SendAdViewEvent(AdId, AdSource, Value, Currency, WatchTime, bReward, OnComplete);
	}
	else
	{
		OnComplete.ExecuteIfBound(false);
	}
}

void UAudiencelabBlueprintLibrary::SendUserMetrics(const UObject* WorldContext, const FString& JsonData, const FOnSDKOperationComplete& OnComplete)
{
	UAudiencelabSDK* SDK = GetAudiencelabSDK(WorldContext);
	if (SDK)
	{
		SDK->SendUserMetrics(JsonData, OnComplete);
	}
	else
	{
		OnComplete.ExecuteIfBound(false);
	}
}

void UAudiencelabBlueprintLibrary::SendCustomPurchaseMetrics(const UObject* WorldContext, const FString& JsonData, const FOnSDKOperationComplete& OnComplete)
{
	UAudiencelabSDK* SDK = GetAudiencelabSDK(WorldContext);
	if (SDK)
	{
		SDK->SendCustomPurchaseMetrics(JsonData, OnComplete);
	}
	else
	{
		OnComplete.ExecuteIfBound(false);
	}
}

void UAudiencelabBlueprintLibrary::SendCustomAdMetrics(const UObject* WorldContext, const FString& JsonData, const FOnSDKOperationComplete& OnComplete)
{
	UAudiencelabSDK* SDK = GetAudiencelabSDK(WorldContext);
	if (SDK)
	{
		SDK->SendCustomAdMetrics(JsonData, OnComplete);
	}
	else
	{
		OnComplete.ExecuteIfBound(false);
	}
}

double UAudiencelabBlueprintLibrary::GetTotalAdValue(const UObject* WorldContext)
{
	UAudiencelabSDK* SDK = GetAudiencelabSDK(WorldContext);
	return SDK ? SDK->GetTotalAdValue() : 0.0;
}

FString UAudiencelabBlueprintLibrary::PurchaseEventToJson(const FAudiencelabPurchaseEvent& PurchaseEvent)
{
	// This function signature is for Blueprint compilation, actual implementation is in execPurchaseEventToJson
	return FString();
}

void UAudiencelabBlueprintLibrary::execPurchaseEventToJson(FFrame& Stack, RESULT_DECL)
{
	// Get the struct property from the stack
	FStructProperty* StructProperty = CastField<FStructProperty>(Stack.MostRecentProperty);
	void* StructPtr = Stack.MostRecentPropertyAddress;
	
	// Advance the stack past the struct parameter
	Stack.Step(Stack.Object(), NULL);
	
	// Get the result parameter
	FString& Result = *(FString*)RESULT_PARAM;
	
	if (StructProperty && StructPtr)
	{
		Result = StructToJson(StructProperty->Struct, StructPtr);
	}
	else
	{
		Result = FString();
	}
}

FString UAudiencelabBlueprintLibrary::AdEventToJson(const FAudiencelabAdEvent& AdEvent)
{
	// This function signature is for Blueprint compilation, actual implementation is in execAdEventToJson
	return FString();
}

void UAudiencelabBlueprintLibrary::execAdEventToJson(FFrame& Stack, RESULT_DECL)
{
	// Get the struct property from the stack
	FStructProperty* StructProperty = CastField<FStructProperty>(Stack.MostRecentProperty);
	void* StructPtr = Stack.MostRecentPropertyAddress;
	
	// Advance the stack past the struct parameter
	Stack.Step(Stack.Object(), NULL);
	
	// Get the result parameter
	FString& Result = *(FString*)RESULT_PARAM;
	
	if (StructProperty && StructPtr)
	{
		Result = StructToJson(StructProperty->Struct, StructPtr);
	}
	else
	{
		Result = FString();
	}
}

FString UAudiencelabBlueprintLibrary::StructToJson(const UStruct* StructType, const void* StructPtr)
{
	if (!StructType || !StructPtr)
	{
		return FString();
	}

	FString JsonString;
	if (FJsonObjectConverter::UStructToJsonObjectString(StructType, StructPtr, JsonString, 0, 0))
	{
		return JsonString;
	}

	return FString();
}