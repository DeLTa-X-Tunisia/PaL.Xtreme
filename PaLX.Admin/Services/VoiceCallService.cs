using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.MixedReality.WebRTC;

namespace PaLX.Admin.Services
{
    public class VoiceCallService
    {
        private PeerConnection _peerConnection;
        private Transceiver _audioTransceiver;
        private HubConnection _hubConnection;
        private string _remoteUser;
        private LocalAudioTrack _localAudioTrack;
        private AudioTrackSource _audioSource;
        private bool _isCallActive = false;
        
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
                await InitializePeerConnection(true);
                _peerConnection.CreateOffer();
            });

            _hubConnection.On<string>("CallDeclined", (sender) => 
            {
                OnCallDeclined?.Invoke(sender);
                EndCall(false);
            });

            _hubConnection.On<string, string, string>("ReceiveOffer", async (sender, sdp, type) => 
            {
                _remoteUser = sender;
                // Ensure PC is initialized (should be if we accepted, but double check)
                await InitializePeerConnection(false);
                
                var sdpMsg = new SdpMessage { Type = SdpMessageType.Offer, Content = sdp };
                await _peerConnection.SetRemoteDescriptionAsync(sdpMsg);
                
                _peerConnection.CreateAnswer();
            });

            _hubConnection.On<string, string, string>("ReceiveAnswer", async (sender, sdp, type) => 
            {
                var sdpMsg = new SdpMessage { Type = SdpMessageType.Answer, Content = sdp };
                await _peerConnection.SetRemoteDescriptionAsync(sdpMsg);
            });

            _hubConnection.On<string, string, int, string>("ReceiveIceCandidate", (sender, candidate, sdpMlineIndex, sdpMid) => 
            {
                if (_peerConnection != null)
                {
                    _peerConnection.AddIceCandidate(new IceCandidate { Content = candidate, SdpMlineIndex = sdpMlineIndex, SdpMid = sdpMid });
                }
            });

            _hubConnection.On<string>("CallEnded", (sender) => 
            {
                EndCall(false);
            });
        }

        public async Task RequestCall(string receiver)
        {
            _remoteUser = receiver;
            await _hubConnection.SendAsync("RequestCall", receiver);
            OnStatusChanged?.Invoke("Appel sortant...");
        }

        public async Task AcceptCall(string sender)
        {
            _isCallActive = true;
            await ApiService.Instance.UpdateStatusAsync(3); // En appel
            _remoteUser = sender;
            await _hubConnection.SendAsync("AcceptCall", sender);
            // Initialize PC immediately to be ready for Offer
            await InitializePeerConnection(false);
            OnStatusChanged?.Invoke("Connexion...");
        }

        public async Task DeclineCall(string sender)
        {
            await _hubConnection.SendAsync("DeclineCall", sender);
            EndCall(false);
        }

        private async Task InitializePeerConnection(bool isCaller)
        {
            if (_peerConnection != null) return;

            try 
            {
                _peerConnection = new PeerConnection();
                
                var config = new PeerConnectionConfiguration
                {
                    IceServers = new List<IceServer> {
                        new IceServer { Urls = { "stun:stun.l.google.com:19302" } }
                    }
                };

                await _peerConnection.InitializeAsync(config);

                _peerConnection.LocalSdpReadytoSend += async (message) => 
                {
                    if (message.Type == SdpMessageType.Offer)
                    {
                        await _hubConnection.SendAsync("SendOffer", _remoteUser, message.Content, message.Type.ToString());
                    }
                    else if (message.Type == SdpMessageType.Answer)
                    {
                        await _hubConnection.SendAsync("SendAnswer", _remoteUser, message.Content, message.Type.ToString());
                    }
                };

                _peerConnection.IceCandidateReadytoSend += async (candidate) => 
                {
                    await _hubConnection.SendAsync("SendIceCandidate", _remoteUser, candidate.Content, candidate.SdpMlineIndex, candidate.SdpMid);
                };

                _peerConnection.IceStateChanged += (newState) => 
                {
                    OnStatusChanged?.Invoke($"Statut: {newState}");
                };

                _peerConnection.Connected += () => 
                {
                    OnStatusChanged?.Invoke("Connecté");
                };

                _peerConnection.AudioTrackAdded += (track) =>
                {
                    OnStatusChanged?.Invoke("Audio distant actif 🔊");
                };

                // Add Audio Track
                _audioSource = await DeviceAudioTrackSource.CreateAsync();
                _localAudioTrack = LocalAudioTrack.CreateFromSource(_audioSource, new LocalAudioTrackInitConfig { });
                
                if (isCaller)
                {
                    _audioTransceiver = _peerConnection.AddTransceiver(MediaKind.Audio);
                    _audioTransceiver.LocalAudioTrack = _localAudioTrack;
                    _audioTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;
                }
                else
                {
                    _peerConnection.TransceiverAdded += (transceiver) =>
                    {
                        if (transceiver.MediaKind == MediaKind.Audio)
                        {
                            _audioTransceiver = transceiver;
                            _audioTransceiver.LocalAudioTrack = _localAudioTrack;
                            _audioTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                var msg = $"Erreur Init: {ex.Message}";
                if (ex.InnerException != null) msg += $"\nInner: {ex.InnerException.Message}";
                OnStatusChanged?.Invoke(msg);
            }
        }

        public void EndCall(bool notifyRemote = true)
        {
            try
            {
                if (_isCallActive)
                {
                    _isCallActive = false;
                    ApiService.Instance.UpdateStatusAsync(0); // Back to Online
                }

                if (notifyRemote && _remoteUser != null)
                {
                    _hubConnection.SendAsync("EndCall", _remoteUser);
                }
                
                _localAudioTrack?.Dispose();
                _localAudioTrack = null;
                
                _audioSource?.Dispose();
                _audioSource = null;
                
                _peerConnection?.Close();
                _peerConnection?.Dispose();
                _peerConnection = null;
                
                OnCallEnded?.Invoke("Appel terminé");
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                Console.WriteLine($"Error ending call: {ex.Message}");
            }
        }
        
        public void ToggleMute(bool isMuted)
        {
            if (_localAudioTrack != null)
            {
                _localAudioTrack.Enabled = !isMuted;
            }
        }
    }
}