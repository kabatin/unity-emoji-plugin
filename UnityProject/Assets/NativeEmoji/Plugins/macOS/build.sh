#!/bin/bash

# macOS用絵文字レンダリングバンドルをビルド
# 使用方法: ./build.sh

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

OUTPUT="EmojiRenderer.bundle"
SOURCE="EmojiRenderer.m"

echo "Building $OUTPUT..."

# バンドルをビルド（x86_64 + arm64 ユニバーサルバイナリ）
# -fobjc-arc は必須。これが無いと MRR でビルドされ、
# alloc/init した NSBitmapImageRep と PNG バッファが呼び出しごとにリークする
clang -dynamiclib \
    -fobjc-arc \
    -mmacosx-version-min=11.0 \
    -arch x86_64 \
    -arch arm64 \
    -framework AppKit \
    -o "$OUTPUT" \
    "$SOURCE"

if [ $? -eq 0 ]; then
    echo "Build successful: $OUTPUT"
    file "$OUTPUT"
else
    echo "Build failed!"
    exit 1
fi
