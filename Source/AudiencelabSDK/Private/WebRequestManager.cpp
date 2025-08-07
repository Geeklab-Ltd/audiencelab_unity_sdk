#include "WebRequestManager.h"
#include "AudiencelabSDKModule.h"
#include "AudiencelabSettings.h"
#include "HttpModule.h"
#include "Json.h"
#include "Engine/GameInstance.h"
#include "Engine/World.h"
#include "Misc/DateTime.h"
#include "Misc/TimeHelper.h"

void UWebRequestManager::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);
	UE_LOG(LogAudiencelabSDK, Log, TEXT("WebRequestManager initialized"));
}

void UWebRequestManager::Deinitialize()
{
	Super::Deinitialize();
	UE_LOG(LogAudiencelabSDK, Log, TEXT("WebRequestManager deinitialized"));
}

UWebRequestManager* UWebRequestManager::Get(const UObject* WorldContext)
{
	if (const UWorld* World = GEngine->GetWorldFromContextObject(WorldContext, EGetWorldErrorMode::LogAndReturnNull))
	{
		if (UGameInstance* GameInstance = World->GetGameInstance())
		{
			return GameInstance->GetSubsystem<UWebRequestManager>();
		}
	}
	return nullptr;
}

void UWebRequestManager::CheckDataCollectionStatus(const FOnHttpRequestComplete& OnSuccess, const FOnHttpRequestError& OnError)
{
	SendRequest(UAudiencelabApiEndpoints::CHECK_DATA_COLLECTION_STATUS, TEXT(""), OnSuccess, OnError, TEXT("GET"));
}

void UWebRequestManager::SendUserMetricsRequest(const FString& JsonData, const FOnHttpRequestComplete& OnSuccess, const FOnHttpRequestError& OnError)
{
	SendWebhookRequest(TEXT("retention"), JsonData, OnSuccess, OnError);
}

void UWebRequestManager::SendAdEventRequest(const FString& JsonData, bool bIsCustom, const FOnHttpRequestComplete& OnSuccess, const FOnHttpRequestError& OnError)
{
	FString Type = bIsCustom ? TEXT("custom.ad") : TEXT("ad");
	SendWebhookRequest(Type, JsonData, OnSuccess, OnError);
}

void UWebRequestManager::SendPurchaseMetricsRequest(const FString& JsonData, bool bIsCustom, const FOnHttpRequestComplete& OnSuccess, const FOnHttpRequestError& OnError)
{
	FString Type = bIsCustom ? TEXT("custom.purchase") : TEXT("purchase");
	SendWebhookRequest(Type, JsonData, OnSuccess, OnError);
}

void UWebRequestManager::VerifyCreativeTokenRequest(const FString& Token, const FOnHttpRequestComplete& OnSuccess, const FOnHttpRequestError& OnError)
{
	TSharedPtr<FJsonObject> JsonObject = MakeShareable(new FJsonObject);
	JsonObject->SetStringField(TEXT("token"), Token);
	
	FString JsonData;
	TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&JsonData);
	FJsonSerializer::Serialize(JsonObject.ToSharedRef(), Writer);
	
	SendRequest(UAudiencelabApiEndpoints::VERIFY_TOKEN, JsonData, OnSuccess, OnError);
}

void UWebRequestManager::FetchTokenRequest(const FOnHttpRequestComplete& OnSuccess, const FOnHttpRequestError& OnError)
{
	// This would need device info - for now, send basic request
	// In a full implementation, this would collect device info like the Unity SDK
	FDateTime CurrentDate = FDateTime::Now();
	FString CurrentDateText = CurrentDate.ToString(TEXT("%Y-%m-%d %H:%M:%S"));

	TSharedPtr<FJsonObject> DeviceData = MakeShareable(new FJsonObject);
	DeviceData->SetStringField(TEXT("device_name"), FPlatformProcess::ComputerName());
	DeviceData->SetNumberField(TEXT("dpi"), 96.0); // Default DPI
	DeviceData->SetStringField(TEXT("os_system"), FPlatformMisc::GetOSVersion());
	
	TSharedPtr<FJsonObject> PostData = MakeShareable(new FJsonObject);
	PostData->SetStringField(TEXT("type"), TEXT("device-metrics"));
	PostData->SetObjectField(TEXT("data"), DeviceData);
	PostData->SetStringField(TEXT("created_at"), CurrentDateText);
	
	FString JsonData;
	TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&JsonData);
	FJsonSerializer::Serialize(PostData.ToSharedRef(), Writer);
	
	SendRequest(UAudiencelabApiEndpoints::FETCH_TOKEN, JsonData, OnSuccess, OnError);
}

void UWebRequestManager::SendWebhookRequest(const FString& Type, const FString& JsonData, const FOnHttpRequestComplete& OnSuccess, const FOnHttpRequestError& OnError)
{
	FDateTime CurrentDate = FDateTime::Now();
	FString CurrentDateText = CurrentDate.ToString(TEXT("%Y-%m-%d %H:%M:%S"));
	FString UtcOffset = GetUtcOffset();

	// Get retention day from config/saved game (equivalent to PlayerPrefs)
	FString RetentionDay = TEXT("");
	// In UE5, you'd use USaveGame or config files instead of PlayerPrefs

	TSharedPtr<FJsonObject> WebhookData = MakeShareable(new FJsonObject);
	WebhookData->SetStringField(TEXT("type"), Type);
	WebhookData->SetStringField(TEXT("created_at"), CurrentDateText);
	WebhookData->SetStringField(TEXT("creativeToken"), TEXT("")); // Would get from token handler
	WebhookData->SetStringField(TEXT("device_name"), FPlatformProcess::ComputerName());
	WebhookData->SetStringField(TEXT("device_model"), FPlatformMisc::GetPlatformName());
	WebhookData->SetStringField(TEXT("os_system"), FPlatformMisc::GetOSVersion());
	WebhookData->SetStringField(TEXT("utc_offset"), UtcOffset);
	WebhookData->SetStringField(TEXT("retention_day"), RetentionDay);
	
	// Parse the incoming JsonData and add as payload
	TSharedPtr<FJsonObject> PayloadObject;
	TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(JsonData);
	if (FJsonSerializer::Deserialize(Reader, PayloadObject) && PayloadObject.IsValid())
	{
		WebhookData->SetObjectField(TEXT("payload"), PayloadObject);
	}
	else
	{
		WebhookData->SetStringField(TEXT("payload"), JsonData);
	}
	
	FString FinalJsonData;
	TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&FinalJsonData);
	FJsonSerializer::Serialize(WebhookData.ToSharedRef(), Writer);
	
	SendRequest(UAudiencelabApiEndpoints::WEBHOOK, FinalJsonData, OnSuccess, OnError);
}

void UWebRequestManager::SendRequest(const FString& Endpoint, const FString& JsonData, const FOnHttpRequestComplete& OnSuccess, const FOnHttpRequestError& OnError, const FString& Method, const TMap<FString, FString>& Headers)
{
	if (!IsInternetAvailable())
	{
		DebugLogError(TEXT("No Internet connection. Please check your connection and try again."), OnError);
		return;
	}

	TSharedRef<IHttpRequest, ESPMode::ThreadSafe> HttpRequest = FHttpModule::Get().CreateRequest();
	HttpRequest->SetURL(Endpoint);
	HttpRequest->SetVerb(Method);
	HttpRequest->SetHeader(TEXT("Content-Type"), TEXT("application/json"));

	// Add API key if available
	const UAudiencelabSettings* Settings = UAudiencelabSettings::Get();
	if (Settings && !Settings->Token.IsEmpty())
	{
		HttpRequest->SetHeader(TEXT("geeklab-api-key"), Settings->Token);
	}

	// Add custom headers
	for (const auto& Header : Headers)
	{
		HttpRequest->SetHeader(Header.Key, Header.Value);
	}

	if (Method != TEXT("GET") && !JsonData.IsEmpty())
	{
		HttpRequest->SetContentAsString(JsonData);
	}

	HttpRequest->OnProcessRequestComplete().BindUObject(this, &UWebRequestManager::OnRequestComplete, OnSuccess, OnError);
	HttpRequest->ProcessRequest();
}

void UWebRequestManager::OnRequestComplete(FHttpRequestPtr Request, FHttpResponsePtr Response, bool bWasSuccessful, FOnHttpRequestComplete OnSuccess, FOnHttpRequestError OnError)
{
	if (bWasSuccessful && Response.IsValid())
	{
		int32 ResponseCode = Response->GetResponseCode();
		FString ResponseString = Response->GetContentAsString();

		if (ResponseCode >= 200 && ResponseCode < 300)
		{
			const UAudiencelabSettings* Settings = UAudiencelabSettings::Get();
			if (Settings && Settings->bShowDebugLog)
			{
				UE_LOG(LogAudiencelabSDK, Log, TEXT("%s Response: %s"), *UAudiencelabSettings::GetDebugLogPrefix(), *ResponseString);
			}
			OnSuccess.ExecuteIfBound(ResponseString);
		}
		else
		{
			FString ErrorMessage;
			switch (ResponseCode)
			{
			case 400:
				ErrorMessage = TEXT("Bad request, data not formatted properly.");
				break;
			case 401:
				ErrorMessage = TEXT("API key is not valid.");
				break;
			case 404:
				ErrorMessage = FString::Printf(TEXT("Not found: %s"), *ResponseString);
				break;
			case 500:
				ErrorMessage = FString::Printf(TEXT("Server error: %s"), *ResponseString);
				break;
			default:
				ErrorMessage = FString::Printf(TEXT("HTTP Error %d: %s"), ResponseCode, *ResponseString);
				break;
			}
			DebugLogError(ErrorMessage, OnError);
		}
	}
	else
	{
		FString ErrorMessage = Request.IsValid() ? Request->GetURL() : TEXT("Unknown request");
		ErrorMessage = FString::Printf(TEXT("Request failed: %s"), *ErrorMessage);
		DebugLogError(ErrorMessage, OnError);
	}
}

FString UWebRequestManager::GetUtcOffset() const
{
	FDateTime UtcNow = FDateTime::UtcNow();
	FDateTime LocalNow = FDateTime::Now();
	FTimespan Offset = LocalNow - UtcNow;

	int32 Hours = Offset.GetHours();
	int32 Minutes = FMath::Abs(Offset.GetMinutes()) % 60;

	return FString::Printf(TEXT("%+03d:%02d"), Hours, Minutes);
}

bool UWebRequestManager::IsInternetAvailable() const
{
	// In UE5, you might want to use a more sophisticated check
	// For now, assume internet is available
	return true;
}

void UWebRequestManager::DebugLogError(const FString& Message, const FOnHttpRequestError& OnError)
{
	const UAudiencelabSettings* Settings = UAudiencelabSettings::Get();
	FString FullMessage = FString::Printf(TEXT("%s %s"), *UAudiencelabSettings::GetDebugLogPrefix(), *Message);
	
	if (OnError.IsBound())
	{
		OnError.ExecuteIfBound(FullMessage);
	}
	else if (Settings && Settings->bShowDebugLog)
	{
		UE_LOG(LogAudiencelabSDK, Warning, TEXT("%s"), *FullMessage);
	}
}