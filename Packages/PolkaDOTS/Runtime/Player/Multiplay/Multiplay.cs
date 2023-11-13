using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PolkaDOTS.Multiplay.MultiplayStats;
using Unity.RenderStreaming;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace PolkaDOTS.Multiplay
{
    /// <summary>
    /// Render stream connection manager, responsible for creating and destroying input and video streams on both
    /// host and guest.
    /// Adapted from RenderStreaming package Multiplay sample.
    /// </summary>
    public class Multiplay : SignalingHandlerBase,
        IOfferHandler, IAddChannelHandler, IDisconnectHandler, IDeletedConnectionHandler, IConnectHandler
    {
        private void Awake()
        {
            defaultCamera.SetActive(false);
            _initialized = false;
        }

        public SignalingManager renderStreaming;
        public GameObject guestPrefab;
        public GameObject playerPrefab;
        public RawImage videoImage;
        public StatsUI statsUI;
        public GameObject defaultCamera;
        private bool _initialized;

        private bool guestConnected = false;
        private SignalingHandlerBase currentHandler;
        private GameObject currentPlayerObj;

        private List<string> connectionIds = new List<string>();
        private List<Component> streams = new List<Component>();
        public HashSet<string> disconnectedIds = new HashSet<string>();

        public Dictionary<string, GameObject> connectionPlayerObjects = new Dictionary<string, GameObject>();

        private MultiplaySettings settings;
        
        private Vector3 initialPosition = new Vector3(0, 40, -4);

        public void InitSettings()
        {
            if (_initialized)
                return;
            
            // Fetch the settings on awake
            MultiplaySettingsManager.Instance.Initialize();
            settings = MultiplaySettingsManager.Instance.Settings;
            _initialized = true;
        }

        public override IEnumerable<Component> Streams => streams;

        public bool IsGuestConnected()
        {
            return guestConnected;
        }

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
            //if (!connectionIds.Contains(connectionId))
            //    return;
            //connectionIds.Remove(connectionId);

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
            //Debug.Log($"Pre-addsender");
            AddSender(data.connectionId, videoChannel);
            //Debug.Log($"Post-addesenderr");
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
            Debug.Log("Creating local player object");
            Cursor.lockState = CursorLockMode.Locked;
            
            // We need to setup local input devices on local players
            var hostPlayerObj = Instantiate(playerPrefab, initialPosition, Quaternion.identity);
            var playerInput = hostPlayerObj.GetComponent<InputReceiver>();
            playerInput.PerformPairingWithAllLocalDevices();
            
            
            connectionPlayerObjects.Add("LOCALPLAYER", hostPlayerObj);

            currentPlayerObj = hostPlayerObj;
        }

        public void SetUpHost(bool cloudOnly = false)
        {
            // Cloud only hosts do not run a local player
            if(!cloudOnly)
                SetUpLocalPlayer();
            
            if (!settings.MultiplayEnabled)
            {
                Debug.Log("SetUpHost called but Multiplay disabled.");
                return;
            }
                
            renderStreaming.useDefaultSettings = false;
            Debug.Log($"Setting up multiplay host with signaling at {settings.SignalingAddress}");
            renderStreaming.SetSignalingSettings(settings.SignalingSettings);
            currentHandler = this;
            statsUI.AddSignalingHandler(this);
            renderStreaming.Run(handlers: new SignalingHandlerBase[] { this });
        }

        public void SetUpGuest(string url)
        {
            if (!settings.MultiplayEnabled)
            {
                Debug.Log("SetUpGuest called but Multiplay disabled.");
                return;
            }
            Debug.Log("Removing any previous connection objects");
            ClearConnectionPlayerObjects();
            settings.SignalingAddress = url;
            StartCoroutine(ConnectGuest());
        }
        
        public void OnConnect(SignalingEventData data)
        {
            //Debug.Log($"Disconnecting {eventData.connectionId}");
            //disconnectedIds.Add(eventData.connectionId);
            //data.connectionId
        }

        IEnumerator ConnectGuest()
        {
            var connectionId = $"{Config.UserID}";//Guid.NewGuid().ToString("N");
            var guestPlayer = Instantiate(guestPrefab);
            var handler = guestPlayer.GetComponent<SingleConnection>();
            statsUI.AddSignalingHandler(handler);
            
            renderStreaming.useDefaultSettings = false;
            renderStreaming.SetSignalingSettings(settings.SignalingSettings);
            Debug.Log($"[{DateTime.Now.TimeOfDay.ToString()}] Setting up multiplay guest with signaling at {settings.SignalingAddress}");
            renderStreaming.Run(handlers: new SignalingHandlerBase[] { handler });
            
            
            
            // Enable the video output
            videoImage.gameObject.SetActive(true);
            var receiveVideoViewer = guestPlayer.GetComponent<VideoStreamReceiver>();
            receiveVideoViewer.OnUpdateReceiveTexture += texture => videoImage.texture = texture;
            
            if (settings != null)
                receiveVideoViewer.SetCodec(settings.ReceiverVideoCodec);
            
            Cursor.lockState = CursorLockMode.Locked;
            
            yield return new WaitUntil(() => handler.WSConnected());
            
            handler.CreateConnection(connectionId);
            
            yield return new WaitUntil(() => handler.IsConnected(connectionId));
            guestConnected = true;
            currentHandler = handler;
            currentPlayerObj = guestPlayer;
        }

        void ClearConnectionPlayerObjects()
        {
            foreach (var (key, connectionPlayerObject) in connectionPlayerObjects)
            {
                Destroy(connectionPlayerObject);
            }
            connectionPlayerObjects.Clear();
        }

        public void StopMultiplay()
        {
            if (guestConnected)
            {
                currentHandler.DeleteConnection($"{Config.UserID}");
            }
            else
            {
                // Stop any ongoing streams
                foreach (var connID in connectionIds)
                {
                    DestroyMultiplayConnection(connID);
                }
                connectionIds.Clear();
                connectionPlayerObjects.Clear();
            }
            // Destroy instantiated gameobjects
            if (currentPlayerObj is not null)
            {
                Destroy(currentPlayerObj);
            }
            statsUI.RemoveSignalingHandler(currentHandler);
            renderStreaming.RemoveSignalingHandler(currentHandler);
            
            // restore default camera setup
            videoImage.gameObject.SetActive(false);
            //defaultCamera.SetActive(true);
        }
    }
    
    

}