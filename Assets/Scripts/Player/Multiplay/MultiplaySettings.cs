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

        private bool useDefaultSettings = true;
        private SignalingType signalingType = SignalingType.WebSocket;
        private string signalingAddress = "localhost";
        private int signalingInterval = 5000;
        private bool signalingSecured = false;
        private Vector2Int streamSize = new Vector2Int(DefaultStreamWidth, DefaultStreamHeight);
        private VideoCodecInfo receiverVideoCodec = null;
        private VideoCodecInfo senderVideoCodec = null;

        public bool UseDefaultSettings
        {
            get { return useDefaultSettings; }
            set { useDefaultSettings = value; }
        }

        public SignalingType SignalingType
        {
            get { return signalingType; }
            set { signalingType = value; }
        }

        public string SignalingAddress
        {
            get { return signalingAddress; }
            set { signalingAddress = value; }
        }

        public bool SignalingSecured
        {
            get { return signalingSecured; }
            set { signalingSecured = value; }
        }

        public int SignalingInterval
        {
            get { return signalingInterval; }
            set { signalingInterval = value; }
        }

        public SignalingSettings SignalingSettings
        {
            get
            {
                switch (signalingType)
                {
                    case SignalingType.WebSocket:
                    {
                        var schema = signalingSecured ? "wss" : "ws";
                        return new WebSocketSignalingSettings
                        (
                            url: $"{schema}://{signalingAddress}"
                        );
                    }
                    case SignalingType.Http:
                    {
                        var schema = signalingSecured ? "https" : "http";
                        return new HttpSignalingSettings
                        (
                            url: $"{schema}://{signalingAddress}",
                            interval: signalingInterval
                        );
                    }
                }

                throw new InvalidOperationException();
            }
        }

        public Vector2Int StreamSize
        {
            get { return streamSize; }
            set { streamSize = value; }
        }

        public VideoCodecInfo ReceiverVideoCodec
        {
            get { return receiverVideoCodec; }
            set { receiverVideoCodec = value; }
        }

        public VideoCodecInfo SenderVideoCodec
        {
            get { return senderVideoCodec; }
            set { senderVideoCodec = value; }
        }
    }

    internal class MultiplaySettingsManager
    {
        static MultiplaySettingsManager s_instance;

        public static MultiplaySettingsManager Instance
        {
            get
            {
                if (s_instance == null)
                    s_instance = new MultiplaySettingsManager();
                return s_instance;
            }
        }

        public MultiplaySettings Settings
        {
            get { return m_settings; }
        }

        MultiplaySettings m_settings;

        public void Initialize()
        {
            if (m_settings == null)
            {
                m_settings = new MultiplaySettings();
                var codecs = VideoStreamReceiver.GetAvailableCodecs();
                bool h264NotFound = true;
                foreach(var codec in codecs)
                {
                    if (codec.name == "H264")
                    {
                        H264CodecInfo  h264 = (H264CodecInfo) codec;
                        if (h264.profile == H264Profile.High)
                        {
                            m_settings.ReceiverVideoCodec = codec;
                            m_settings.SenderVideoCodec = codec;
                            h264NotFound = false;
                            break;
                        }
                    }
                }
                if(h264NotFound)
                    Debug.LogWarning($"Could not find H264.High as streaming codec!");
            }
        }
    }
}