// // InstalledFonts.m
// #import <UIKit/UIKit.h>

// extern "C" {

// const char *GetInstalledFonts()
// {
//     NSArray<NSString*>* fontFamilyNames = [UIFont familyNames];
//     NSString* joinedNames = [fontFamilyNames componentsJoinedByString:@","];
//     char* returnValue = (char*) malloc(sizeof(char) * joinedNames.length + 1);
//     strcpy(returnValue, [joinedNames UTF8String]);
//     return returnValue;
// }

// }


char *_GetInstalledFonts(){
    NSArray<NSString*>* fontFamilyNames = [UIFont familyNames];
    NSString* joinedNames = [fontFamilyNames componentsJoinedByString:@","];
    char* returnValue = (char*) malloc(sizeof(char) * joinedNames.length + 1);
    strcpy(returnValue, [joinedNames UTF8String]);
    return returnValue;
}