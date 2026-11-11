package com.nativeemoji;

import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Rect;
import android.util.Log;
import java.io.ByteArrayOutputStream;

/**
 * Android native emoji renderer for Unity
 * Renders emoji using system font and returns PNG byte array
 */
public class EmojiRenderer {

    private static final String TAG = "NativeEmoji";

    /**
     * Render emoji to PNG byte array
     * @param emoji The emoji string to render
     * @param width Target width in pixels
     * @param height Target height in pixels
     * @return PNG byte array or null on failure
     */
    public static byte[] renderEmoji(String emoji, int width, int height) {
        if (emoji == null || emoji.isEmpty() || width <= 0 || height <= 0) {
            return null;
        }

        try {
            Bitmap bitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888);
            Canvas canvas = new Canvas(bitmap);

            Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
            paint.setTextSize(height * 0.85f);
            paint.setTextAlign(Paint.Align.CENTER);

            Rect bounds = new Rect();
            paint.getTextBounds(emoji, 0, emoji.length(), bounds);

            float x = width / 2f;
            float y = (height - bounds.top - bounds.bottom) / 2f;

            canvas.drawText(emoji, x, y, paint);

            ByteArrayOutputStream stream = new ByteArrayOutputStream();
            bitmap.compress(Bitmap.CompressFormat.PNG, 100, stream);
            byte[] result = stream.toByteArray();

            stream.close();
            bitmap.recycle();

            return result;

        } catch (Exception e) {
            // 黙って null を返すと Unity 側ではフォールバックのグレー四角になり、
            // 原因が追えなくなるのでログに残す
            Log.e(TAG, "renderEmoji failed for \"" + emoji + "\" (" + width + "x" + height + ")", e);
            return null;
        }
    }
}
