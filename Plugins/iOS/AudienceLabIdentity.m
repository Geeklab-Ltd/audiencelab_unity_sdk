#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

const char *_GetIDFV()
{
    @autoreleasepool {
        NSUUID *idfv = [[UIDevice currentDevice] identifierForVendor];
        if (idfv == nil) {
            return NULL;
        }

        NSString *idfvString = [idfv UUIDString];
        const char *utf8 = [idfvString UTF8String];
        if (utf8 == NULL) {
            return NULL;
        }

        char *returnValue = (char *)malloc(strlen(utf8) + 1);
        strcpy(returnValue, utf8);
        return returnValue;
    }
}
