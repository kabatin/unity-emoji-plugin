#import <AppKit/AppKit.h>

/**
 * macOS用絵文字レンダリング関数
 * Unity Editor (macOS) から P/Invoke で呼び出される
 *
 * 出力は必ず width x height ピクセル。
 * NSImage の lockFocus はディスプレイの backing scale を掛けてしまい、
 * Retina 環境で要求の2倍の解像度が返るため、ピクセル数を明示した
 * NSBitmapImageRep に直接描画している（iOS/Android の実装と挙動を揃える）。
 */
const char* RenderEmojiMac(const char* emoji, int width, int height, int* dataLength) {
    @autoreleasepool {
        if (dataLength == NULL) {
            return NULL;
        }

        *dataLength = 0;

        if (emoji == NULL || width <= 0 || height <= 0) {
            return NULL;
        }

        NSString *text = [NSString stringWithUTF8String:emoji];
        if (text == nil || text.length == 0) {
            return NULL;
        }

        // ピクセル数を明示したビットマップを用意する
        NSBitmapImageRep *bitmap = [[NSBitmapImageRep alloc]
            initWithBitmapDataPlanes:NULL
                          pixelsWide:width
                          pixelsHigh:height
                       bitsPerSample:8
                     samplesPerPixel:4
                            hasAlpha:YES
                            isPlanar:NO
                      colorSpaceName:NSCalibratedRGBColorSpace
                         bytesPerRow:width * 4
                        bitsPerPixel:32];

        if (bitmap == nil) {
            return NULL;
        }

        // アルファはプリマルチプライ（bitmapFormat 0）のままにする。
        // NSBitmapFormatAlphaNonpremultiplied を指定すると
        // graphicsContextWithBitmapImageRep: が nil を返して描画できない。
        // PNG エンコード時にストレートアルファへ戻される。
        NSGraphicsContext *context = [NSGraphicsContext graphicsContextWithBitmapImageRep:bitmap];
        if (context == nil) {
            return NULL;
        }

        [NSGraphicsContext saveGraphicsState];
        [NSGraphicsContext setCurrentContext:context];

        // 背景を透明にクリア
        [[NSColor clearColor] set];
        NSRectFill(NSMakeRect(0, 0, width, height));

        // システムフォント（Apple Color Emoji へフォールバックする）
        CGFloat fontSize = height * 0.85;
        NSFont *font = [NSFont systemFontOfSize:fontSize];
        NSDictionary *attributes = @{ NSFontAttributeName: font };

        // テキストサイズを計算してセンタリング
        NSSize textSize = [text sizeWithAttributes:attributes];
        CGFloat x = (width - textSize.width) / 2;
        CGFloat y = (height - textSize.height) / 2;

        [text drawAtPoint:NSMakePoint(x, y) withAttributes:attributes];

        [context flushGraphics];
        [NSGraphicsContext restoreGraphicsState];

        // PNGにエンコード
        NSData *pngData = [bitmap representationUsingType:NSBitmapImageFileTypePNG properties:@{}];
        if (pngData == nil) {
            return NULL;
        }

        // Unity が Marshal.Copy するまでバイト列を生かしておく。
        // ARC では static の代入で前回分が解放される（build.sh の -fobjc-arc が前提）。
        static NSData *staticData = nil;
        staticData = pngData;
        *dataLength = (int)[staticData length];

        return (const char*)[staticData bytes];
    }
}
