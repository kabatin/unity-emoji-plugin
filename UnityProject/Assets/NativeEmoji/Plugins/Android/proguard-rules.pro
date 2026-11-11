# Keep EmojiRenderer class for Unity JNI calls
-keep class com.nativeemoji.EmojiRenderer {
    public static byte[] renderEmoji(java.lang.String, int, int);
}
