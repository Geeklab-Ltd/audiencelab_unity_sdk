#include "AudiencelabSDKModule.h"

#define LOCTEXT_NAMESPACE "FAudiencelabSDKModule"

DEFINE_LOG_CATEGORY(LogAudiencelabSDK);

void FAudiencelabSDKModule::StartupModule()
{
	// This code will execute after your module is loaded into memory; the exact timing is specified in the .uplugin file per-module
	UE_LOG(LogAudiencelabSDK, Log, TEXT("AudiencelabSDK module started"));
}

void FAudiencelabSDKModule::ShutdownModule()
{
	// This function may be called during shutdown to clean up your module.  For modules that support dynamic reloading,
	// we call this function before unloading the module.
	UE_LOG(LogAudiencelabSDK, Log, TEXT("AudiencelabSDK module stopped"));
}

#undef LOCTEXT_NAMESPACE
	
IMPLEMENT_MODULE(FAudiencelabSDKModule, AudiencelabSDK)