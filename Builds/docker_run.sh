docker run -it \
--name=opencraft2-client \
--net=host \
--rm=true \
-v ./logs:/opencraft2/logs/ \
jerriteic/opencraft2:base sh -c "./opencraft2.x86_64 -playType Client \
 -nographics -batchmode \
 -multiplayRole Guest \
 -signalingUrl ws://127.0.0.1:7981 \
 -localConfigJson ./localconfig.json \
 -logFile ./logs/opencraft2_log.txt \
 -duration 30 "

