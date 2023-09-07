docker run -it \
  --runtime=nvidia \
  --name=super-container \
  --security-opt seccomp=unconfined \
  --init \
  --net=host \
  --privileged=true \
  --rm=false \
  -e DISPLAY=:0 \
  -v /tmp/.X11-unix/X0:/tmp/.X11-unix/X0:ro \
  -v /etc/localtime:/etc/localtime:ro \
  -v ./logs:/opencraft2/logs \
  jerriteic/opencraft2:base
