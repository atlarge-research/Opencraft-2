using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.NetCode.LowLevel.Unsafe;
using Unity.RenderStreaming;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Adapted from RenderStreaming package Multiplay sample
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

    void Awake()
    {
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

        var obj = connectionPlayerObjects[connectionId];
        var sender = obj.GetComponent<StreamSenderBase>();
        var inputChannel = obj.GetComponent<InputReceiver>();
        //var multiplayChannel = obj.GetComponentInChildren<MultiplayChannel>();

        connectionPlayerObjects.Remove(connectionId);
        UnityEngine.Object.Destroy(obj);

        RemoveSender(connectionId, sender);
        RemoveChannel(connectionId, inputChannel);
        //RemoveChannel(connectionId, multiplayChannel);

        streams.Remove(sender);
        streams.Remove(inputChannel);
        //streams.Remove(multiplayChannel);

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
        
        // Spawn object with camera and input component
        var initialPosition = new Vector3(0, 3, 0);
        var playerObj = Instantiate(playerPrefab, initialPosition, Quaternion.identity);
        connectionPlayerObjects.Add(data.connectionId, playerObj);

        var videoChannel = playerObj.GetComponent<StreamSenderBase>();

        if(videoChannel is VideoStreamSender videoStreamSender && settings != null)
        {
            videoStreamSender.width = (uint)settings.StreamSize.x;
            videoStreamSender.height = (uint)settings.StreamSize.y;
            videoStreamSender.SetCodec(settings.SenderVideoCodec);
        }

        var inputChannel = playerObj.GetComponent<InputReceiver>();
        //var multiplayChannel = newObj.GetComponentInChildren<MultiplayChannel>();
        //var playerController = newObj.GetComponentInChildren<PlayerController>();

        /*if (multiplayChannel.OnChangeLabel == null)
            multiplayChannel.OnChangeLabel = new ChangeLabelEvent();
        multiplayChannel.OnChangeLabel.AddListener(playerController.SetLabel);*/

        streams.Add(videoChannel);
        streams.Add(inputChannel);
        //streams.Add(multiplayChannel);

        AddSender(data.connectionId, videoChannel);
        AddChannel(data.connectionId, inputChannel);
        //AddChannel(data.connectionId, multiplayChannel);

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
        // host player
        var initialPosition = new Vector3(0, 4, -4);
        var hostPlayer = GameObject.Instantiate(playerPrefab, initialPosition, Quaternion.identity);
        var playerController = hostPlayer.GetComponent<MultiplayPlayerController>();
        //playerController.SetLabel(username);
        var playerInput = hostPlayer.GetComponent<InputReceiver>();
        playerInput.PerformPairingWithAllLocalDevices();
        playerController.CheckPairedDevices();
        connectionPlayerObjects.Add("LOCALPLAYER", hostPlayer);
    }
    
    
    public void SetUpHost()
    {
       SetUpLocalPlayer();
        
        if (settings != null)
            renderStreaming.useDefaultSettings = settings.UseDefaultSettings;
        if (settings?.SignalingSettings != null)
            renderStreaming.SetSignalingSettings(settings.SignalingSettings);
        renderStreaming.Run(handlers: new SignalingHandlerBase[] {this});
    }

    public void SetUpGuest()
    {
        StartCoroutine(ConnectGuest());
    }
    
    IEnumerator ConnectGuest( )
    {
        var connectionId = Guid.NewGuid().ToString("N");
        var guestPlayer = GameObject.Instantiate(guestPrefab);
        var handler = guestPlayer.GetComponent<SingleConnection>();

        //statsUI.AddSignalingHandler(handler);
        if (settings != null)
            renderStreaming.useDefaultSettings = settings.UseDefaultSettings;
        if (settings?.SignalingSettings != null)
            renderStreaming.SetSignalingSettings(settings.SignalingSettings);
        renderStreaming.Run(handlers: new SignalingHandlerBase[] {handler});

        videoImage.gameObject.SetActive(true);
        var receiveVideoViewer = guestPlayer.GetComponent<VideoStreamReceiver>();
        receiveVideoViewer.OnUpdateReceiveTexture += texture => videoImage.texture = texture;

        /*var channel = guestPlayer.GetComponent<MultiplayChannel>();
        channel.OnStartedChannel += _ => { StartCoroutine(ChangeLabel(channel, username)); };*/

        if(settings != null)
            receiveVideoViewer.SetCodec(settings.ReceiverVideoCodec);

        // todo fix this hacky wait for remote hosting client initialization
        yield return new WaitForSeconds(1f);

        handler.CreateConnection(connectionId);
        yield return new WaitUntil(() => handler.IsConnected(connectionId));
    }
}

