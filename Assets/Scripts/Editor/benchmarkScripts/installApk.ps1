param (
    [Parameter(Mandatory=$true)][string]$apk
)
adb pm uninstall -k --user 0 com.AtlargeResearch.Opencraft2

echo $apk
adb install $apk
adb shell am start com.AtlargeResearch.Opencraft2/com.unity3d.player.UnityPlayerActivity