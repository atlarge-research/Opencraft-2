param (
    [Parameter(Mandatory=$true)][string]$executable
)
ssh joachim@192.168.23.94 "pkill -SIGKILL linux.x86_64"
echo $executable
ssh joachim@192.168.23.94 "DISPLAY=:0 nohup ~/Opencraft2/universal/linux.x86_64 -deploymentID 0 -deploymentJson ~/Opencraft2/deploymentSing.json -renderDist $executable -logStats True -statsFile ~/Opencraft2/stats$executable.csv -playerFly"