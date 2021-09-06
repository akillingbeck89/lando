using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Photon.Realtime;
using Photon.Pun;
using Photon;
using ExitGames.Client.Photon;

public class NetworkManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public const string kEpisodeKey = "episode";
    public const string kEpisodeNodeKey = "node";
    public const string kNodeStateKey = "node-state";

    [SerializeField] GameManager gameManager_;
    [SerializeField] GameManager.Type type_;

    public const byte kNewEpisodeCode = 1;
    public const byte kNewEpisodeNodeCode = 2;
    public const byte kNewEpisodeNodeStateCode= 3;

    // Start is called before the first frame update
    void Start()
    {
        gameManager_.Init(this);
    }

    public void SendNewEpisodeMessage(string e)
    {
        object[] content = new object[] { e };
        RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(kNewEpisodeCode, content, raiseEventOptions, SendOptions.SendReliable);

        UpdateRoomState(episode: e, node:"", state:GameManager.NodeState.Playing);
    }

    public void SendNewEpisodeNodeMessage(string n)
    {
        object[] content = new object[] { n };
        RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(kNewEpisodeNodeCode, content, raiseEventOptions, SendOptions.SendReliable);

        UpdateRoomState(node: n);
    }

    public void SendNewNodeStateMessage(string s)
    {
        object[] content = new object[] { s };
        RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(NetworkManager.kNewEpisodeNodeStateCode, content, raiseEventOptions, SendOptions.SendReliable);

        UpdateRoomState(state: s);
    }

    private void UpdateRoomState(string episode = null, string node = null, string state = null)
    {
        ExitGames.Client.Photon.Hashtable h = PhotonNetwork.CurrentRoom.CustomProperties;
        if (episode != null)
        {
            h[kEpisodeKey] = episode;
        }
        if (node != null)
        {
            h[kEpisodeNodeKey] = node;
        }
        if (state != null)
        {
            h[kNodeStateKey] = state;
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(h);
    }

    public void OnEvent(EventData photonEvent)
    {
        byte eventCode = photonEvent.Code;
        object[] data = (object[])photonEvent.CustomData;

        if (photonEvent.CustomData != null && eventCode < 200)
        {
            Debug.Log(string.Format("Received event: {0} {1}", eventCode.ToString(), data[0].ToString()));
        }

        if (eventCode == kNewEpisodeCode)
        {
            string episode = (string)data[0];
            gameManager_.NewEpisodeEvent(episode);

        } else if (eventCode == kNewEpisodeNodeCode)
        {
            string node = (string)data[0];
            gameManager_.NewNodeEvent(node);
        } else if (eventCode == kNewEpisodeNodeStateCode)
        {
            string state = (string)data[0];
            gameManager_.NewStateEvent(state);
        }
    }

    public override void OnPlayerEnteredRoom(Player other)
    {
        Debug.Log(string.Format("Player entered room. Current player count is {0}", PhotonNetwork.CurrentRoom.PlayerCount));
    }

    public override void OnPlayerLeftRoom(Player other)
    {
        Debug.Log(string.Format("Player left room. Current player count is {0}", PhotonNetwork.CurrentRoom.PlayerCount));
    }
}