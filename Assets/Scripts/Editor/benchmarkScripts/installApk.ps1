param (
    [Parameter(Mandatory=$true)][string]$apk
)

echo $apk
adb install $apk
adb shell am start com.AtlargeResearch.Opencraft2/com.unity3d.player.UnityPlayerActivity