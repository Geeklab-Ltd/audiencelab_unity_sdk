#import <UIKit/UIKit.h>

#ifdef __cplusplus
extern "C" {
#endif

char *_GetNativeScreenWidth() {
    CGRect screenBounds = [UIScreen mainScreen].bounds;
    CGFloat scale = [UIScreen mainScreen].scale;
    NSString *widthString = [NSString stringWithFormat:@"%f", screenBounds.size.width * scale];
    char *returnValue = (char *)malloc(sizeof(char) * (widthString.length + 1));
    strcpy(returnValue, [widthString UTF8String]);
    return returnValue;
}

char *_GetNativeScreenHeight() {
    CGRect screenBounds = [UIScreen mainScreen].bounds;
    CGFloat scale = [UIScreen mainScreen].scale;
    NSString *heightString = [NSString stringWithFormat:@"%f", screenBounds.size.height * scale];
    char *returnValue = (char *)malloc(sizeof(char) * (heightString.length + 1));
    strcpy(returnValue, [heightString UTF8String]);
    return returnValue;
}

#ifdef __cplusplus
}
#endif