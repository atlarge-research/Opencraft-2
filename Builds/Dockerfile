FROM jerriteic/gpu_ubuntu20.04

# Select which build application folder to include
ARG BUILD_TO_COPY=./ServerClientLinuxDebug
# Identify signaling webserver
ARG WEBSERVER_LOCATION=./Multiplay_WebApp/server

# Install encode and decode libraries used for Multiplay
#RUN apt-get update \
#  && apt-get install -y -qq --no-install-recommends \
#    libnvidia-encode-535-server \
#    libnvidia-decode-535-server \
#  && rm -rf /var/lib/apt/lists/*
  
# Copy game files
COPY $BUILD_TO_COPY /usr/src/opencraft2

# Copy signalling server
COPY $WEBSERVER_LOCATION /usr/src/opencraft2

# Set working directory
WORKDIR /usr/src/opencraft2

# Unrestricted env vars for the nvidia-container-runtime.
ENV NVIDIA_VISIBLE_DEVICES all
ENV NVIDIA_DRIVER_CAPABILITIES all

# Disable QT applications from using X's shared memory
#ENV QT_X11_NO_MITSHM=1

# Debug port
EXPOSE 59820
# Deployment port
EXPOSE 7980
# Game port
EXPOSE 7979
# Signaling port
EXPOSE 7981

#CMD ["./opencraft2.x86_64", "-playType", "ClientAndServer", "-logFile", "./opencraft2_log.txt"]
#"-nographics", "-batchmode"
CMD /bin/bash
