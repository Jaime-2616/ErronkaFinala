using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cliente.Services
{
    public class ChatService : IDisposable
    {
        // Zerbitzariaren IP eta ataka
        private readonly string _serverIp = "127.0.0.1";
        private readonly int _serverPort = 5000;
        private readonly string _username;

        TcpClient? _client;
        NetworkStream? _stream;
        CancellationTokenSource? _cts;
        Task? _receiveTask;

        // Mezuak eta erronkak jasotzeko ekitaldiak
        public event Action<string, string>? MessageReceived;
        public event Action<string>? ChallengeReceived;
        public event Action<string, string>? BattleStarted;
        public event Action<string>? ChallengeRejected;
        public event Action<string, string>? RivalTeamSelected;
        public event Action<string>? SurrenderReceived;
        public event Action<string, int>? RivalMoveSelected; // aurkariak mugimendua aukeratu du
        public event Action<string>? BattleEnded; // borroka amaitu da

        public ChatService(string username)
        {
            _username = username;
        }

        // Konexioa hasieratzen du eta jasotze-loop-a abiarazten du
        public void Start()
        {
            _cts = new CancellationTokenSource();
            _client = new TcpClient();
            _client.Connect(_serverIp, _serverPort);
            _stream = _client.GetStream();

            SendRaw($"subscribe|{_username}");
            _receiveTask = Task.Run(() => ReceiveLoop(_cts.Token), _cts.Token);
        }

        // Testu-mezua bidaltzen du
        public void SendMessage(string text)
        {
            if (_stream == null || !_stream.CanWrite) return;
            SendRaw($"chat|{text}");
        }

        // Erronka bat bidaltzen du beste jokalari bati
        public void SendChallenge(string targetUser)
        {
            if (_stream == null || !_stream.CanWrite) return;
            SendRaw($"challenge|{_username}|{targetUser}");
        }

        // Erronkari erantzuten dio (onartu/ukatu)
        public void SendChallengeResponse(string challengerUser, bool accept)
        {
            if (_stream == null || !_stream.CanWrite) return;
            string decision = accept ? "ACCEPT" : "REJECT";
            SendRaw($"challenge_response|{_username}|{challengerUser}|{decision}");
        }

        // Taldearen hautaketa bidaltzen du
        public void SendTeamSelected(string player1, string player2, string teamName)
        {
            if (_stream == null || !_stream.CanWrite) return;
            SendRaw($"team_selected|{player1}|{player2}|{teamName}");
        }

        // Errendizioa bidaltzen du
        public void SendSurrender(string toUser, int aliveFrom, int aliveTo)
        {
            // surrender|fromUser|toUser|aliveFrom|aliveTo
            SendRaw($"surrender|{_username}|{toUser}|{aliveFrom}|{aliveTo}");
        }

        // Mugimendu aukeratua bidaltzen du
        public void SendMoveSelected(string rivalUser, int moveSlot)
        {
            if (_stream == null || !_stream.CanWrite) return;
            SendRaw($"move_selected|{_username}|{rivalUser}|{moveSlot}");
        }

        // Borroka amaieraren informazioa bidaltzen du
        public void SendBattleEnd(string winner, string player1, string player2, int alive1, int alive2)
        {
            // battle_end|winner|player1|player2|alive1|alive2
            SendRaw($"battle_end|{winner}|{player1}|{player2}|{alive1}|{alive2}");
        }

        // Datu gordina bidaltzen du zerbitzarira
        void SendRaw(string line)
        {
            if (_stream == null || !_stream.CanWrite) return;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(line + "\n");
                _stream.Write(data, 0, data.Length);
                _stream.Flush();
            }
            catch { }
        }

        // Mezuak jasotzeko loop nagusia
        async Task ReceiveLoop(CancellationToken token)
        {
            var buffer = new byte[4096];
            var sb = new StringBuilder();
            try
            {
                while (!token.IsCancellationRequested && _stream != null)
                {
                    int read = await _stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (read <= 0) break;

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
                    string content = sb.ToString();
                    int newlineIndex;
                    while ((newlineIndex = content.IndexOf('\n')) >= 0)
                    {
                        string line = content[..newlineIndex].Trim('\r');
                        content = content[(newlineIndex + 1)..];
                        ProcessLine(line);
                    }
                    sb.Clear();
                    sb.Append(content);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        // Jasotako lerro bakoitza prozesatzen du eta dagokion ekitaldia aktibatzen du
        void ProcessLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            var parts = line.Split('|');
            if (parts.Length == 0) return;

            switch (parts[0])
            {
                case "MSG" when parts.Length >= 3:
                {
                    var sender = parts[1];
                    var text = string.Join("|", parts, 2, parts.Length - 2);
                    MessageReceived?.Invoke(sender, text);
                    break;
                }
                case "CHALLENGE" when parts.Length >= 2:
                {
                    var fromUser = parts[1];
                    ChallengeReceived?.Invoke(fromUser);
                    break;
                }
                case "BATTLE_START" when parts.Length >= 3:
                {
                    var challenger = parts[1];
                    var responder = parts[2];
                    BattleStarted?.Invoke(challenger, responder);
                    break;
                }
                case "CHALLENGE_REJECTED" when parts.Length >= 2:
                {
                    var byUser = parts[1];
                    ChallengeRejected?.Invoke(byUser);
                    break;
                }
                case "RIVAL_TEAM" when parts.Length >= 3:
                {
                    var rivalUser = parts[1];
                    var teamName = parts[2];
                    RivalTeamSelected?.Invoke(rivalUser, teamName);
                    break;
                }
                case "SURRENDER" when parts.Length >= 2:
                {
                    var fromUser = parts[1];
                    SurrenderReceived?.Invoke(fromUser);
                    break;
                }
                // Aurkariak mugimendua aukeratu du
                case "RIVAL_MOVE" when parts.Length >= 3:
                {
                    var fromUser = parts[1];
                    if (int.TryParse(parts[2], out int slot))
                        RivalMoveSelected?.Invoke(fromUser, slot);
                    break;
                }
                // Borroka amaitu da
                case "BATTLE_END" when parts.Length >= 2:
                    {
                        var winner = parts[1];
                        BattleEnded?.Invoke(winner);
                        break;
                    }
                default:
                    break;
            }
        }

        // Konexioa eteten du eta baliabideak askatzen ditu
        public void Disconnect()
        {
            try { _cts?.Cancel(); } catch { }
            Dispose();
        }

        // Baliabideak garbitzen ditu
        public void Dispose()
        {
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
            _stream = null;
            _client = null;
            try { _cts?.Dispose(); } catch { }
            _cts = null;
        }
    }
}