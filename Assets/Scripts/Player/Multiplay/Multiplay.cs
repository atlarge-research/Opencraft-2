using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.RenderStreaming;
using UnityEngine;
using UnityEngine.UI;

namespace Opencraft.Player.Multiplay
{
    // Render stream connection manager, responsible for creating and destroying input and video streams on both
    // host and guest.
    // Adapted from RenderStreaming package Multiplay sample.
    public class Multiplay : SignalingHandlerBase,
        IOfferHandler, IAddChannelHandler, IDisconnectHandler, IDeletedConnectionHandler
    {
        public SignalingManager renderStreaming;
        public GameObject guestPrefab;
        public GameObject playerPrefab;
        public RawImage videoImage;
        public GameObject defaultCamera;

        private List<string> connectionIds = new List<string>();
        private List<Component> streams = new List<Component>();
        public HashSet<string> disconnectedIds = new HashSet<string>();

        public Dictionary<string, GameObject> connectionPlayerObjects = new Dictionary<string, GameObject>();

        private MultiplaySettings settings;
        
        private Vector3 initialPosition = new Vector3(0, 3, -4);

        void Awake()
        {
            // Fetch the settings on awake
            MultiplaySettingsManager.Instance.Initialize();
            settings = MultiplaySettingsManager.Instance.Settings;
        }

        public override IEnumerable<Component> Streams => streams;


        // On delete or disconnect, simply mark this connection for destruction. Handle destruction in MultiplayPlayerSystem
        public void OnDeletedConnection(SignalingEventData eventData)
        {
            Debug.Log($"Disconnecting {eventData.connectionId}");
            disconnectedIds.Add(eventData.connectionId);
        }

        public void OnDisconnect(SignalingEventData eventData)
        {
            Debug.Log($"Disconnecting {eventData.connectionId}");
            disconnectedIds.Add(eventData.connectionId);
        }

        public void DestroyMultiplayConnection(string connectionId)
        {
            if (!connectionIds.Contains(connectionId))
                return;
            connectionIds.Remove(connectionId);

            var playerObject = connectionPlayerObjects[connectionId];
            var sender = playerObject.GetComponent<StreamSenderBase>();
            var inputChannel = playerObject.GetComponent<InputReceiver>();


            connectionPlayerObjects.Remove(connectionId);
            Destroy(playerObject);

            RemoveSender(connectionId, sender);
            RemoveChannel(connectionId, inputChannel);

            streams.Remove(sender);
            streams.Remove(inputChannel);

            if (ExistConnection(connectionId))
                DeleteConnection(connectionId);
        }


        public void OnOffer(SignalingEventData data)
        {
            Debug.Log($"Received streaming connection with id {data.connectionId}");
            if (connectionIds.Contains(data.connectionId))
            {
                Debug.Log($"Already answered this connectionId : {data.connectionId}");
                return;
            }

            connectionIds.Add(data.connectionId);

            // Spawn object with camera and input component at a default location. This object will be synced
            // with player transform on clients by PlayerInputSystem. These objects do not exist on the server.
            var playerObj = Instantiate(playerPrefab, initialPosition, Quaternion.identity);
            connectionPlayerObjects.Add(data.connectionId, playerObj);

            var videoChannel = playerObj.GetComponent<StreamSenderBase>();

            if (videoChannel is VideoStreamSender videoStreamSender && settings != null)
            {
                videoStreamSender.width = (uint)settings.StreamSize.x;
                videoStreamSender.height = (uint)settings.StreamSize.y;
                videoStreamSender.SetCodec(settings.SenderVideoCodec);
            }

            var inputChannel = playerObj.GetComponent<InputReceiver>();

            streams.Add(videoChannel);
            streams.Add(inputChannel);

            AddSender(data.connectionId, videoChannel);
            AddChannel(data.connectionId, inputChannel);

            SendAnswer(data.connectionId);
        }

        public void OnAddChannel(SignalingEventData data)
        {
            var obj = connectionPlayerObjects[data.connectionId];
            var channels = obj.GetComponents<IDataChannel>();
            var channel = channels.FirstOrDefault(_ => !_.IsLocal && !_.IsConnected);
            channel?.SetChannel(data);
        }

        public void SetUpLocalPlayer()
        {
            defaultCamera.SetActive(false);
            // We need to setup local input devices on local players
            var hostPlayerObj = Instantiate(playerPrefab, initialPosition, Quaternion.identity);
            var playerController = hostPlayerObj.GetComponent<MultiplayPlayerController>();
            var playerInput = hostPlayerObj.GetComponent<InputReceiver>();
            playerInput.PerformPairingWithAllLocalDevices();
            playerController.CheckPairedDevices();
            connectionPlayerObjects.Add("LOCALPLAYER", hostPlayerObj);
        }

        public void SetUpHost()
        {
            SetUpLocalPlayer();

            if (settings != null)
                renderStreaming.useDefaultSettings = settings.UseDefaultSettings;
            if (settings?.SignalingSettings != null)
                renderStreaming.SetSignalingSettings(settings.SignalingSettings);
            renderStreaming.Run(handlers: new SignalingHandlerBase[] { this });
        }

        public void SetUpGuest()
        {
            StartCoroutine(ConnectGuest());
        }

        IEnumerator ConnectGuest()
        {
            var connectionId = Guid.NewGuid().ToString("N");
            var guestPlayer = Instantiate(guestPrefab);
            var handler = guestPlayer.GetComponent<SingleConnection>();
            
            if (settings != null)
                renderStreaming.useDefaultSettings = settings.UseDefaultSettings;
            if (settings?.SignalingSettings != null)
                renderStreaming.SetSignalingSettings(settings.SignalingSettings);
            renderStreaming.Run(handlers: new SignalingHandlerBase[] { handler });
            
            // Enable the video output
            videoImage.gameObject.SetActive(true);
            var receiveVideoViewer = guestPlayer.GetComponent<VideoStreamReceiver>();
            receiveVideoViewer.OnUpdateReceiveTexture += texture => videoImage.texture = texture;
            
            if (settings != null)
                receiveVideoViewer.SetCodec(settings.ReceiverVideoCodec);

            handler.CreateConnection(connectionId);
            yield return new WaitUntil(() => handler.IsConnected(connectionId));
        }
    }

}