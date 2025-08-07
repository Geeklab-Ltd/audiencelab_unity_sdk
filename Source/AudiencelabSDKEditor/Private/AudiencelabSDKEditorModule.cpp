#include "AudiencelabSDKEditorModule.h"
#include "AudiencelabSettings.h"
#include "ISettingsModule.h"
#include "ISettingsSection.h"

#define LOCTEXT_NAMESPACE "FAudiencelabSDKEditorModule"

void FAudiencelabSDKEditorModule::StartupModule()
{
	// This code will execute after your module is loaded into memory; the exact timing is specified in the .uplugin file per-module
	RegisterSettings();
}

void FAudiencelabSDKEditorModule::ShutdownModule()
{
	// This function may be called during shutdown to clean up your module.  For modules that support dynamic reloading,
	// we call this function before unloading the module.
	UnregisterSettings();
}

void FAudiencelabSDKEditorModule::RegisterSettings()
{
	if (ISettingsModule* SettingsModule = FModuleManager::GetModulePtr<ISettingsModule>("Settings"))
	{
		TSharedPtr<ISettingsSection> SettingsSection = SettingsModule->RegisterSettings("Project", "Plugins", "Audiencelab SDK",
			LOCTEXT("AudiencelabSDKSettingsName", "Audiencelab SDK"),
			LOCTEXT("AudiencelabSDKSettingsDescription", "Configure settings for the Audiencelab SDK plugin"),
			GetMutableDefault<UAudiencelabSettings>()
		);

		if (SettingsSection.IsValid())
		{
			SettingsSection->OnModified().BindRaw(this, &FAudiencelabSDKEditorModule::OnSettingsModified);
		}
	}
}

void FAudiencelabSDKEditorModule::UnregisterSettings()
{
	if (ISettingsModule* SettingsModule = FModuleManager::GetModulePtr<ISettingsModule>("Settings"))
	{
		SettingsModule->UnregisterSettings("Project", "Plugins", "Audiencelab SDK");
	}
}

bool FAudiencelabSDKEditorModule::OnSettingsModified()
{
	// Handle settings changes if needed
	return true;
}

#undef LOCTEXT_NAMESPACE
	
IMPLEMENT_MODULE(FAudiencelabSDKEditorModule, AudiencelabSDKEditor)