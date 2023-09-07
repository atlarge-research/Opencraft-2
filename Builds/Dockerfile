FROM jerriteic/gpu_ubuntu20.04

# Select which build application folder to include
ARG BUILD_TO_COPY=./ServerClientLinuxDebug
# Identify signaling webserver
ARG WEBSERVER_LOCATION=./Multiplay_WebApp/server

# Copy game files
COPY $BUILD_TO_COPY /opencraft2
# Copy signalling server
COPY $WEBSERVER_LOCATION /opencraft2

# Set working directory
WORKDIR /opencraft2

# Unrestricted env vars for the nvidia-container-runtime.
ENV NVIDIA_VISIBLE_DEVICES all
ENV NVIDIA_DRIVER_CAPABILITIES all


# Game port
EXPOSE 7979
# Deployment port
EXPOSE 7980
# Signaling port
EXPOSE 7981

# We run the game from the kubernetes job description
CMD /bin/bash
