#include "AudiencelabSDK.h"
#include "AudiencelabSDKModule.h"
#include "AudiencelabSettings.h"
#include "WebRequestManager.h"
#include "Engine/GameInstance.h"
#include "Engine/World.h"
#include "Json.h"
#include "GameFramework/SaveGame.h"
#include "Kismet/GameplayStatics.h"

const FString UAudiencelabSDK::TOTAL_AD_VALUE_KEY = TEXT("AudiencelabSDK_TotalAdValue");

void UAudiencelabSDK::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);
	InitializeSDK();
}

void UAudiencelabSDK::Deinitialize()
{
	Super::Deinitialize();
	UE_LOG(LogAudiencelabSDK, Log, TEXT("AudiencelabSDK deinitialized"));
}

UAudiencelabSDK* UAudiencelabSDK::Get(const UObject* WorldContext)
{
	if (const UWorld* World = GEngine->GetWorldFromContextObject(WorldContext, EGetWorldErrorMode::LogAndReturnNull))
	{
		if (UGameInstance* GameInstance = World->GetGameInstance())
		{
			return GameInstance->GetSubsystem<UAudiencelabSDK>();
		}
	}
	return nullptr;
}

void UAudiencelabSDK::InitializeSDK()
{
	const UAudiencelabSettings* Settings = UAudiencelabSettings::Get();
	if (!Settings)
	{
		UE_LOG(LogAudiencelabSDK, Warning, TEXT("AudiencelabSDK settings not found"));
		return;
	}

	if (!Settings->IsFullyEnabled())
	{
		UE_LOG(LogAudiencelabSDK, Log, TEXT("AudiencelabSDK is disabled or not configured"));
		return;
	}

	UE_LOG(LogAudiencelabSDK, Log, TEXT("%s SDK Initialized!"), *UAudiencelabSettings::GetDebugLogPrefix());
}

FString UAudiencelabSDK::GetCreativeToken() const
{
	// In a full implementation, this would get the token from a handler
	// For now, return cached or empty
	return CachedCreativeToken;
}

FString UAudiencelabSDK::GetDeepLink() const
{
	// In a full implementation, this would get the deep link from a handler
	// For now, return cached or empty
	return CachedDeepLink;
}

void UAudiencelabSDK::ToggleMetricsCollection(bool bIsEnabled)
{
	// In UE5, we can't modify the settings at runtime like Unity's PlayerPrefs
	// This would require a different approach, perhaps saving to a game save
	UE_LOG(LogAudiencelabSDK, Log, TEXT("Metrics collection toggle: %s"), bIsEnabled ? TEXT("Enabled") : TEXT("Disabled"));
}

bool UAudiencelabSDK::GetIsMetricsCollection() const
{
	const UAudiencelabSettings* Settings = UAudiencelabSettings::Get();
	return Settings ? Settings->IsMetricsEnabled() : false;
}

void UAudiencelabSDK::SendUserMetrics(const FString& JsonData, const FOnSDKOperationComplete& OnComplete)
{
	if (!IsConfigFullyEnabled())
	{
		OnComplete.ExecuteIfBound(false);
		return;
	}

	UWebRequestManager* WebManager = UWebRequestManager::Get(this);
	if (!WebManager)
	{
		OnComplete.ExecuteIfBound(false);
		return;
	}

	FOnHttpRequestComplete OnSuccess;
	OnSuccess.BindUFunction(this, FName("OnUserMetricsSuccess"));
	
	FOnHttpRequestError OnError;
	OnError.BindUFunction(this, FName("OnUserMetricsError"));

	WebManager->SendUserMetricsRequest(JsonData, OnSuccess, OnError);
}

void UAudiencelabSDK::SendCustomPurchaseMetrics(const FString& JsonData, const FOnSDKOperationComplete& OnComplete)
{
	if (!IsConfigFullyEnabled())
	{
		OnComplete.ExecuteIfBound(false);
		return;
	}

	UWebRequestManager* WebManager = UWebRequestManager::Get(this);
	if (!WebManager)
	{
		OnComplete.ExecuteIfBound(false);
		return;
	}

	FOnHttpRequestComplete OnSuccess;
	OnSuccess.BindLambda([OnComplete](const FString& Response)
	{
		OnComplete.ExecuteIfBound(true);
	});
	
	FOnHttpRequestError OnError;
	OnError.BindLambda([OnComplete](const FString& Error)
	{
		OnComplete.ExecuteIfBound(false);
	});

	WebManager->SendPurchaseMetricsRequest(JsonData, true, OnSuccess, OnError);
}

void UAudiencelabSDK::SendCustomPurchaseEvent(const FString& ItemId, const FString& ItemName, double Value, const FString& Currency, const FString& Status, const FOnSDKOperationComplete& OnComplete)
{
	if (!IsConfigFullyEnabled())
	{
		OnComplete.ExecuteIfBound(false);
		return;
	}

	const UAudiencelabSettings* Settings = UAudiencelabSettings::Get();
	if (Settings && Settings->bShowDebugLog)
	{
		UE_LOG(LogAudiencelabSDK, Log, TEXT("%s Sending custom purchase event"), *UAudiencelabSettings::GetDebugLogPrefix());
	}

	// Create JSON object for purchase event
	TSharedPtr<FJsonObject> JsonObject = MakeShareable(new FJsonObject);
	JsonObject->SetStringField(TEXT("item_id"), ItemId);
	JsonObject->SetStringField(TEXT("item_name"), ItemName);
	JsonObject->SetNumberField(TEXT("value"), Value);
	JsonObject->SetStringField(TEXT("currency"), Currency);
	JsonObject->SetStringField(TEXT("status"), Status);

	FString JsonData;
	TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&JsonData);
	FJsonSerializer::Serialize(JsonObject.ToSharedRef(), Writer);

	SendCustomPurchaseMetrics(JsonData, OnComplete);
}

void UAudiencelabSDK::SendCustomAdMetrics(const FString& JsonData, const FOnSDKOperationComplete& OnComplete)
{
	if (!IsConfigFullyEnabled())
	{
		OnComplete.ExecuteIfBound(false);
		return;
	}

	UWebRequestManager* WebManager = UWebRequestManager::Get(this);
	if (!WebManager)
	{
		OnComplete.ExecuteIfBound(false);
		return;
	}

	FOnHttpRequestComplete OnSuccess;
	OnSuccess.BindLambda([OnComplete](const FString& Response)
	{
		OnComplete.ExecuteIfBound(true);
	});
	
	FOnHttpRequestError OnError;
	OnError.BindLambda([OnComplete](const FString& Error)
	{
		OnComplete.ExecuteIfBound(false);
	});

	WebManager->SendAdEventRequest(JsonData, true, OnSuccess, OnError);
}

void UAudiencelabSDK::SendCustomAdEvent(const FString& AdId, const FString& Name, const FString& Source, int32 WatchTime, bool bReward, const FString& MediaSource, const FString& Channel, double Value, const FString& Currency, const FOnSDKOperationComplete& OnComplete)
{
	if (!IsConfigFullyEnabled())
	{
		OnComplete.ExecuteIfBound(false);
		return;
	}

	const UAudiencelabSettings* Settings = UAudiencelabSettings::Get();
	if (Settings && Settings->bShowDebugLog)
	{
		UE_LOG(LogAudiencelabSDK, Log, TEXT("%s Sending custom ad event"), *UAudiencelabSettings::GetDebugLogPrefix());
	}

	// Add to the cumulative ad value
	double TotalAdValue = AddToTotalAdValue(Value);

	// Create JSON object for ad event
	TSharedPtr<FJsonObject> JsonObject = MakeShareable(new FJsonObject);
	JsonObject->SetStringField(TEXT("ad_id"), AdId);
	JsonObject->SetStringField(TEXT("name"), Name);
	JsonObject->SetStringField(TEXT("source"), Source);
	JsonObject->SetNumberField(TEXT("watch_time"), WatchTime);
	JsonObject->SetBoolField(TEXT("reward"), bReward);
	JsonObject->SetStringField(TEXT("media_source"), MediaSource);
	JsonObject->SetStringField(TEXT("channel"), Channel);
	JsonObject->SetNumberField(TEXT("value"), Value);
	JsonObject->SetStringField(TEXT("currency"), Currency);
	JsonObject->SetNumberField(TEXT("total_ad_value"), TotalAdValue);

	FString JsonData;
	TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&JsonData);
	FJsonSerializer::Serialize(JsonObject.ToSharedRef(), Writer);

	SendCustomAdMetrics(JsonData, OnComplete);
}

void UAudiencelabSDK::SendAdViewEvent(const FString& AdId, const FString& AdSource, double Value, const FString& Currency, int32 WatchTime, bool bReward, const FOnSDKOperationComplete& OnComplete)
{
	if (!IsConfigFullyEnabled())
	{
		OnComplete.ExecuteIfBound(false);
		return;
	}

	const UAudiencelabSettings* Settings = UAudiencelabSettings::Get();
	if (Settings && Settings->bShowDebugLog)
	{
		UE_LOG(LogAudiencelabSDK, Log, TEXT("%s Sending ad view event"), *UAudiencelabSettings::GetDebugLogPrefix());
	}

	// Add to the cumulative ad value
	double TotalAdValue = AddToTotalAdValue(Value);

	// Create JSON object for ad view event
	TSharedPtr<FJsonObject> JsonObject = MakeShareable(new FJsonObject);
	JsonObject->SetStringField(TEXT("ad_id"), AdId);
	JsonObject->SetStringField(TEXT("name"), TEXT("ad_view"));
	JsonObject->SetStringField(TEXT("source"), AdSource);
	JsonObject->SetNumberField(TEXT("watch_time"), WatchTime);
	JsonObject->SetBoolField(TEXT("reward"), bReward);
	JsonObject->SetNumberField(TEXT("value"), Value);
	JsonObject->SetStringField(TEXT("currency"), Currency);
	JsonObject->SetNumberField(TEXT("total_ad_value"), TotalAdValue);

	FString JsonData;
	TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&JsonData);
	FJsonSerializer::Serialize(JsonObject.ToSharedRef(), Writer);

	SendCustomAdMetrics(JsonData, OnComplete);
}

double UAudiencelabSDK::GetTotalAdValue() const
{
	// In UE5, we use config files or save games instead of Unity's PlayerPrefs
	// For simplicity, using GConfig to read from game config
	float SavedValue = 0.0f;
	GConfig->GetFloat(TEXT("AudiencelabSDK"), *TOTAL_AD_VALUE_KEY, SavedValue, GGameUserSettingsIni);
	return static_cast<double>(SavedValue);
}

bool UAudiencelabSDK::IsConfigFullyEnabled() const
{
	const UAudiencelabSettings* Settings = UAudiencelabSettings::Get();
	if (!Settings)
	{
		return false;
	}

	if (!Settings->IsMetricsEnabled())
	{
		if (Settings->bShowDebugLog)
		{
			if (!Settings->bSendStatistics)
			{
				UE_LOG(LogAudiencelabSDK, Warning, TEXT("This option is disabled in the settings! Please enable it in Project Settings -> Plugins -> Audiencelab SDK"));
			}
			else
			{
				UE_LOG(LogAudiencelabSDK, Warning, TEXT("AudiencelabSDK is disabled! To work with the SDK, please enable it in Project Settings -> Plugins -> Audiencelab SDK"));
			}
		}
		return false;
	}

	return true;
}

double UAudiencelabSDK::AddToTotalAdValue(double AdValue)
{
	double CurrentTotal = GetTotalAdValue();
	double NewTotal = CurrentTotal + AdValue;
	
	// Save to config file
	GConfig->SetFloat(TEXT("AudiencelabSDK"), *TOTAL_AD_VALUE_KEY, static_cast<float>(NewTotal), GGameUserSettingsIni);
	GConfig->Flush(false, GGameUserSettingsIni);

	const UAudiencelabSettings* Settings = UAudiencelabSettings::Get();
	if (Settings && Settings->bShowDebugLog)
	{
		UE_LOG(LogAudiencelabSDK, Log, TEXT("%s Total ad value updated by %f to: %f"), *UAudiencelabSettings::GetDebugLogPrefix(), AdValue, NewTotal);
	}

	return NewTotal;
}