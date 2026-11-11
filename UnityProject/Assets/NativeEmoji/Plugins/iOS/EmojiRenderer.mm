#import <UIKit/UIKit.h>

/**
 * iOS native emoji renderer for Unity
 * Renders emoji using system font and returns PNG byte array
 */
extern "C" {

const char* _RenderEmoji(const char* emoji, int width, int height, int* dataLength) {
    @autoreleasepool {
        if (emoji == NULL || width <= 0 || height <= 0) {
            *dataLength = 0;
            return NULL;
        }

        NSString *text = [NSString stringWithUTF8String:emoji];
        if (text == nil || text.length == 0) {
            *dataLength = 0;
            return NULL;
        }

        CGSize size = CGSizeMake(width, height);
        CGFloat fontSize = height * 0.85;

        UIFont *font = [UIFont systemFontOfSize:fontSize];

        UIGraphicsBeginImageContextWithOptions(size, NO, 1.0);
        CGContextRef context = UIGraphicsGetCurrentContext();

        if (context == NULL) {
            UIGraphicsEndImageContext();
            *dataLength = 0;
            return NULL;
        }

        CGContextClearRect(context, CGRectMake(0, 0, width, height));

        NSDictionary *attributes = @{
            NSFontAttributeName: font
        };

        CGSize textSize = [text sizeWithAttributes:attributes];
        CGFloat x = (size.width - textSize.width) / 2;
        CGFloat y = (size.height - textSize.height) / 2;

        [text drawAtPoint:CGPointMake(x, y) withAttributes:attributes];

        UIImage *image = UIGraphicsGetImageFromCurrentImageContext();
        UIGraphicsEndImageContext();

        if (image == nil) {
            *dataLength = 0;
            return NULL;
        }

        NSData *pngData = UIImagePNGRepresentation(image);
        if (pngData == nil) {
            *dataLength = 0;
            return NULL;
        }

        // Unity が Marshal.Copy するまでバイト列を生かしておく。
        // Objective-C オブジェクトで保持すると Xcode 側の ARC 設定次第で
        // 解放されたりリークしたりするため、malloc で自前管理する。
        static void *staticBuffer = NULL;

        if (staticBuffer != NULL) {
            free(staticBuffer);
            staticBuffer = NULL;
        }

        NSUInteger length = [pngData length];
        staticBuffer = malloc(length);

        if (staticBuffer == NULL) {
            *dataLength = 0;
            return NULL;
        }

        memcpy(staticBuffer, [pngData bytes], length);
        *dataLength = (int)length;

        return (const char*)staticBuffer;
    }
}

}
