#!/usr/bin/env bash
# Build Gear VR Guard without Gradle: javac -> d8 -> aapt2 -> zipalign -> apksigner.
# Output: tools/gearvr-guard/out/gearvr-guard.apk
# Signing key: %USERPROFILE%\.android\gearvr-guard.keystore (created on first run,
# kept OUTSIDE the repo; standard public debug password, not a secret).
set -euo pipefail
cd "$(dirname "$0")"

SDK="${ANDROID_SDK:-$LOCALAPPDATA/Android/Sdk}"
BT="$SDK/build-tools/36.1.0"
JAR="$SDK/platforms/android-34/android.jar"
JDK="${JDK:-$LOCALAPPDATA/Programs/Android Studio/jbr}"
KS="$USERPROFILE/.android/gearvr-guard.keystore"

rm -rf out && mkdir -p out/classes out/dex
"$JDK/bin/javac" -source 8 -target 8 -nowarn -Xlint:-options \
    -classpath "$JAR" -d out/classes $(find src -name '*.java')
"$BT/d8.bat" --min-api 24 --lib "$JAR" --output out/dex $(find out/classes -name '*.class')
"$BT/aapt2.exe" link -I "$JAR" --manifest AndroidManifest.xml \
    --min-sdk-version 24 --target-sdk-version 29 -o out/unsigned.apk
python -I -c "import zipfile,sys; zipfile.ZipFile(sys.argv[1],'a',zipfile.ZIP_DEFLATED).write(sys.argv[2],'classes.dex')" out/unsigned.apk out/dex/classes.dex
"$BT/zipalign.exe" -f 4 out/unsigned.apk out/aligned.apk

if [ ! -f "$KS" ]; then
    "$JDK/bin/keytool" -genkeypair -keystore "$KS" -storepass android -keypass android \
        -alias guard -keyalg RSA -keysize 2048 -validity 10000 \
        -dname "CN=Gear VR Guard,O=local" >/dev/null 2>&1
fi
"$BT/apksigner.bat" sign --ks "$KS" --ks-pass pass:android --key-pass pass:android \
    --out out/gearvr-guard.apk out/aligned.apk
"$BT/apksigner.bat" verify out/gearvr-guard.apk
echo "Built $(pwd)/out/gearvr-guard.apk"
