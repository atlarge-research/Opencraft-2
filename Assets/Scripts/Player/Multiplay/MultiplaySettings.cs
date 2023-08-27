using System;
using Unity.RenderStreaming;
using UnityEngine;

namespace Opencraft.Player.Multiplay
{
    public enum SignalingType
    {
        WebSocket,
        Http
    }

    // Multiplay settings class, has the default values
    public class MultiplaySettings
    {
        public const int DefaultStreamWidth = 1920;
        public const int DefaultStreamHeight = 1080;

        public bool MultiplayEnabled { get; set; } = true;

        public SignalingType SignalingType { get; set; } = SignalingType.WebSocket;

        public string SignalingAddress { get; set; } = "127.0.0.1";

        public ushort SignalingPort { get; set; } = 7981;

        public bool SignalingSecured { get; set; } = false;

        public int SignalingInterval { get; set; } = 5000;

        public SignalingSettings SignalingSettings
        {
            get
            {
                switch (SignalingType)
                {
                    case SignalingType.WebSocket:
                    {
                        var schema = SignalingSecured ? "wss" : "ws";
                        return new WebSocketSignalingSettings
                        (
                            url: $"{schema}://{SignalingAddress}:{SignalingPort}"
                        );
                    }
                    case SignalingType.Http:
                    {
                        var schema = SignalingSecured ? "https" : "http";
                        return new HttpSignalingSettings
                        (
                            url: $"{schema}://{SignalingAddress}:{SignalingPort}",
                            interval: SignalingInterval
                        );
                    }
                }

                throw new InvalidOperationException();
            }
        }

        public Vector2Int StreamSize { get; set; } = new Vector2Int(DefaultStreamWidth, DefaultStreamHeight);

        public VideoCodecInfo ReceiverVideoCodec { get; set; } = null;

        public VideoCodecInfo SenderVideoCodec { get; set; } = null;
    }

    internal class MultiplaySettingsManager
    {
        static MultiplaySettingsManager s_instance;

        public static MultiplaySettingsManager Instance
        {
            get { return s_instance ??= new MultiplaySettingsManager(); }
        }

        public MultiplaySettings Settings { get; private set; }

        public void Initialize()
        {
            if (Settings != null) return;
            Settings = new MultiplaySettings();
            var codecs = VideoStreamReceiver.GetAvailableCodecs();
            bool h264NotFound = true;
            VideoCodecInfo chosenCodec = null;
            foreach(var codec in codecs)
            {
                if (codec.name == "AV1")
                {
                    AV1CodecInfo av1 = (AV1CodecInfo) codec;
                    Debug.Log($"Found codec: AV1 {av1.profile}");
                }else if (codec.name == "VP9")
                {
                    VP9CodecInfo vp9 = (VP9CodecInfo)codec;
                    Debug.Log($"Found codec: VP9 {vp9.profile}");
                } else if (codec.name == "H264")
                {
                    H264CodecInfo h264 = (H264CodecInfo) codec;
                    Debug.Log($"Found codec: H264 {h264.profile} {h264.level}");
                    if (h264.profile == H264Profile.High)
                    {
                        h264NotFound = false;
                        chosenCodec = codec;
                    }
                }
                else
                {
                    Debug.Log($"Found non-specified codec: {codec.name} {codec.codecImplementation} {codec.mimeType} {codec.sdpFmtpLine}");
                }
                // Backup codec if we don't find h264.high
                if (h264NotFound)
                    chosenCodec = codec;
            }
            if(chosenCodec == null){
                Debug.LogError($"No supported codec types found! Disabling Multiplay");
                Settings.MultiplayEnabled = false;
            }
            if(h264NotFound)
                Debug.LogWarning($"Could not find H264.High as streaming codec!");
            Settings.ReceiverVideoCodec = chosenCodec;
            Settings.SenderVideoCodec = chosenCodec;
            Settings.SignalingAddress = Config.SignalingUrl;
            Settings.SignalingPort    = Config.SignalingPort;
        }
    }
}