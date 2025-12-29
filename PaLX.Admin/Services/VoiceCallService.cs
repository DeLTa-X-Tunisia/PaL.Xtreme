using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.MixedReality.WebRTC;

namespace PaLX.Admin.Services
{
    public class VoiceCallService
    {
        private ConcurrentDictionary<string, PeerConnection> _peerConnections = new ConcurrentDictionary<string, PeerConnection>();
        private ConcurrentDictionary<string, Transceiver> _audioTransceivers = new ConcurrentDictionary<string, Transceiver>();
        private ConcurrentDictionary<string, LocalAudioTrack> _localAudioTracks = new ConcurrentDictionary<string, LocalAudioTrack>();
        
        private HubConnection _hubConnection;
        private AudioTrackSource _audioSource;
        private bool _isCallActive = false;
        private bool _isMuted = false;
        private SemaphoreSlim _audioLock = new SemaphoreSlim(1, 1);
        
        public event Action<string> OnCallEnded;
        public event Action<string> OnStatusChanged;
        public event Action<string> OnIncomingCall;
        public event Action<string> OnCallAccepted;
        public event Action<string> OnCallDeclined;

        public VoiceCallService(HubConnection hubConnection)
        {
            _hubConnection = hubConnection;
            InitializeSignalR();
        }

        private void InitializeSignalR()
        {
            _hubConnection.On<string>("IncomingCallRequest", (sender) => 
            {
                if (_isCallActive)
                {
                    _hubConnection.SendAsync("DeclineCall", sender);
                    return;
                }
                OnIncomingCall?.Invoke(sender);
            });

            _hubConnection.On<string>("CallAccepted", async (sender) => 
            {
                _isCallActive = true;
                ApiService.Instance.UpdateStatusAsync(3); // En appel
                OnCallAccepted?.Invoke(sender);
                // User accepted, start the WebRTC negotiation
                await InitializePeerConnection(sender, true);
                if (_peerConnections.TryGetValue(sender, out var pc))
                {
                    pc.CreateOffer();
                }
            });

            _hubConnection.On<string>("CallDeclined", (sender) => 
            {
                OnCallDeclined?.Invoke(sender);
                EndCall(sender, false);
            });

            _hubConnection.On<string, string, string>("ReceiveOffer", async (sender, sdp, type) => 
            {
                // Ensure PC is initialized
                await InitializePeerConnection(sender, false);
                
                if (_peerConnections.TryGetValue(sender, out var pc))
                {
                    var sdpMsg = new SdpMessage { Type = SdpMessageType.Offer, Content = sdp };
                    await pc.SetRemoteDescriptionAsync(sdpMsg);
                    pc.CreateAnswer();
                }
            });

            _hubConnection.On<string, string, string>("ReceiveAnswer", async (sender, sdp, type) => 
            {
                if (_peerConnections.TryGetValue(sender, out var pc))
                {
                    var sdpMsg = new SdpMessage { Type = SdpMessageType.Answer, Content = sdp };
                    await pc.SetRemoteDescriptionAsync(sdpMsg);
                }
            });

            _hubConnection.On<string, string, int, string>("ReceiveIceCandidate", (sender, candidate, sdpMlineIndex, sdpMid) => 
            {
                if (_peerConnections.TryGetValue(sender, out var pc))
                {
                    pc.AddIceCandidate(new IceCandidate { Content = candidate, SdpMlineIndex = sdpMlineIndex, SdpMid = sdpMid });
                }
            });

            _hubConnection.On<string>("CallEnded", (sender) => 
            {
                EndCall(sender, false);
            });
        }

        public async Task RequestCall(string receiver)
        {
            await _hubConnection.SendAsync("RequestCall", receiver);
            OnStatusChanged?.Invoke($"Appel vers {receiver}...");
        }

        public async Task AcceptCall(string sender)
        {
            _isCallActive = true;
            await ApiService.Instance.UpdateStatusAsync(3); // En appel
            await _hubConnection.SendAsync("AcceptCall", sender);
            // Initialize PC immediately to be ready for Offer
            await InitializePeerConnection(sender, false);
            OnStatusChanged?.Invoke("Connexion...");
        }

        public async Task DeclineCall(string sender)
        {
            await _hubConnection.SendAsync("DeclineCall", sender);
            // If we have no other calls, end everything?
            // For decline, we just don't start.
        }

        private async Task InitializePeerConnection(string remoteUser, bool isCaller)
        {
            if (_peerConnections.ContainsKey(remoteUser)) return;

            try 
            {
                var pc = new PeerConnection();
                
                var config = new PeerConnectionConfiguration
                {
                    IceServers = new List<IceServer> {
                        new IceServer { Urls = { "stun:stun.l.google.com:19302" } }
                    }
                };

                await pc.InitializeAsync(config);

                pc.LocalSdpReadytoSend += async (message) => 
                {
                    if (message.Type == SdpMessageType.Offer)
                    {
                        await _hubConnection.SendAsync("SendOffer", remoteUser, message.Content, message.Type.ToString());
                    }
                    else if (message.Type == SdpMessageType.Answer)
                    {
                        await _hubConnection.SendAsync("SendAnswer", remoteUser, message.Content, message.Type.ToString());
                    }
                };

                pc.IceCandidateReadytoSend += async (candidate) => 
                {
                    await _hubConnection.SendAsync("SendIceCandidate", remoteUser, candidate.Content, candidate.SdpMlineIndex, candidate.SdpMid);
                };

                pc.IceStateChanged += (newState) => 
                {
                    OnStatusChanged?.Invoke($"Statut ({remoteUser}): {newState}");
                };

                pc.Connected += () => 
                {
                    OnStatusChanged?.Invoke($"Connecté avec {remoteUser}");
                };

                pc.AudioTrackAdded += (track) =>
                {
                    OnStatusChanged?.Invoke($"Audio de {remoteUser} actif 🔊");
                };

                // Add Audio Track
                await _audioLock.WaitAsync();
                LocalAudioTrack localTrack = null;
                try
                {
                    if (_audioSource == null)
                    {
                        _audioSource = await DeviceAudioTrackSource.CreateAsync();
                    }
                    // Create a NEW track for THIS connection
                    localTrack = LocalAudioTrack.CreateFromSource(_audioSource, new LocalAudioTrackInitConfig { });
                    localTrack.Enabled = !_isMuted;
                    _localAudioTracks.TryAdd(remoteUser, localTrack);
                }
                finally
                {
                    _audioLock.Release();
                }
                
                if (isCaller)
                {
                    var transceiver = pc.AddTransceiver(MediaKind.Audio);
                    transceiver.LocalAudioTrack = localTrack;
                    transceiver.DesiredDirection = Transceiver.Direction.SendReceive;
                    _audioTransceivers.TryAdd(remoteUser, transceiver);
                }
                else
                {
                    pc.TransceiverAdded += (transceiver) =>
                    {
                        if (transceiver.MediaKind == MediaKind.Audio)
                        {
                            transceiver.LocalAudioTrack = localTrack;
                            transceiver.DesiredDirection = Transceiver.Direction.SendReceive;
                            _audioTransceivers.TryAdd(remoteUser, transceiver);
                        }
                    };
                }

                _peerConnections.TryAdd(remoteUser, pc);
            }
            catch (Exception ex)
            {
                var msg = $"Erreur Init ({remoteUser}): {ex.Message}";
                if (ex.InnerException != null) msg += $"\nInner: {ex.InnerException.Message}";
                OnStatusChanged?.Invoke(msg);
            }
        }

        public void EndCall(string? remoteUser = null, bool notifyRemote = true)
        {
            try
            {
                if (remoteUser != null)
                {
                    // End specific call
                    if (_peerConnections.TryRemove(remoteUser, out var pc))
                    {
                        if (notifyRemote) _hubConnection.SendAsync("EndCall", remoteUser);
                        pc.Close();
                        pc.Dispose();
                    }
                    _audioTransceivers.TryRemove(remoteUser, out _);
                    
                    if (_localAudioTracks.TryRemove(remoteUser, out var track))
                    {
                        track.Dispose();
                    }
                }
                else
                {
                    // End ALL calls
                    foreach (var kvp in _peerConnections)
                    {
                        if (notifyRemote) _hubConnection.SendAsync("EndCall", kvp.Key);
                        kvp.Value.Close();
                        kvp.Value.Dispose();
                    }
                    _peerConnections.Clear();
                    _audioTransceivers.Clear();
                    
                    foreach (var track in _localAudioTracks.Values)
                    {
                        track.Dispose();
                    }
                    _localAudioTracks.Clear();
                }

                if (_peerConnections.IsEmpty)
                {
                    _isCallActive = false;
                    ApiService.Instance.UpdateStatusAsync(0); // Back to Online
                    
                    _audioLock.Wait();
                    try
                    {
                        _audioSource?.Dispose();
                        _audioSource = null;
                    }
                    finally
                    {
                        _audioLock.Release();
                    }
                    
                    OnCallEnded?.Invoke("Appel terminé");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error ending call: {ex.Message}");
            }
        }
        
        public async Task ConnectToPeer(string username)
        {
            if (_peerConnections.ContainsKey(username)) return;
            
            _isCallActive = true; 
            await InitializePeerConnection(username, true);
            if (_peerConnections.TryGetValue(username, out var pc))
            {
                pc.CreateOffer();
            }
        }

        public void DisconnectPeer(string username)
        {
            EndCall(username, false);
        }

        public void SetMute(bool isMuted)
        {
            _isMuted = isMuted;
            foreach (var track in _localAudioTracks.Values)
            {
                track.Enabled = !isMuted;
            }
        }

        public void ToggleMute(bool isMuted) => SetMute(isMuted);

        public void Dispose()
        {
            try
            {
                foreach (var pc in _peerConnections.Values)
                {
                    pc.Close();
                    pc.Dispose();
                }
                _peerConnections.Clear();

                foreach (var track in _localAudioTracks.Values)
                {
                    track.Dispose();
                }
                _localAudioTracks.Clear();

                _audioSource?.Dispose();
                _audioSource = null;
                
                _audioLock?.Dispose();
            }
            catch { }
        }
    }
}