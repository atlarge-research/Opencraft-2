param (
    [Parameter(Mandatory=$true)][string]$executable
)

# echo "#" + $executable + "#"
# ssh -t joachim@192.168.23.94 ls
# ssh -t joachim@192.168.23.94 "DISPLAY=:0 /home/joachim/Opencraft2/"
# ssh -t joachim@192.168.23.94 "DISPLAY=:0 /home/joachim/Opencraft2/rd-3"
#echo $executable > ggdafile.txt
ssh joachim@192.168.23.94 "DISPLAY=:0 nohup $executable -deploymentID 1 -remoteConfig -deploymentURL 192.168.23.115 -logStats -statsFile $executable.csv -profiler-enable -profiler-log-file $executable.raw"