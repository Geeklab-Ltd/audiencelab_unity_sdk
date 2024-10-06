#import <sys/utsname.h>

const char *_GetDeviceModel() 
{
    struct utsname systemInfo;
    uname(&systemInfo);

    NSString *deviceModel = [NSString stringWithCString:systemInfo.machine encoding:NSUTF8StringEncoding];
    
    char* returnValue = (char*) malloc(sizeof(char) * deviceModel.length + 1);
    strcpy(returnValue, [deviceModel UTF8String]);
    return returnValue;
}
