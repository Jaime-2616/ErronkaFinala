using System;
using System.Net.Sockets;
using System.Text;

namespace Cliente.Services
{
    public static class ServerService
    {
        // Zerbitzariaren IP eta ataka
        private const string serverIp = "127.0.0.1";
        private const int serverPort = 5000;

        // Uneko erabiltzailearen izena (login ondoren ezarri)
        public static string CurrentUsername { get; set; } = string.Empty;

        // Zerbitzarira eskaera bat bidaltzen du eta erantzuna jasotzen du
        public static string SendRequest(string action, string p1, string p2)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.Connect(serverIp, serverPort);
                    NetworkStream stream = client.GetStream();

                    // Mezua era egokian prestatzen eta bidaltzen du
                    string message = $"{action}|{p1}|{p2}\n";
                    Console.WriteLine("Enviando: [" + message.Replace("\n", "\\n") + "]");
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    stream.Write(data, 0, data.Length);
                    stream.Flush();

                    // Zerbitzariaren erantzuna jasotzen du
                    var sb = new StringBuilder();
                    byte[] buffer = new byte[1024];
                    int bytesRead;
                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                        if (sb.ToString().IndexOf('\n') >= 0) break;
                    }

                    string response = sb.ToString();
                    int nl = response.IndexOf('\n');
                    if (nl >= 0) response = response.Substring(0, nl);
                    Console.WriteLine("Respuesta recibida: [" + response + "]");
                    return response;
                }
            }
            catch (Exception ex)
            {
                // Konexioan errorea gertatuz gero, errore-mezua bueltatzen du
                return "ERROR|No se pudo conectar al servidor: " + ex.Message;
            }
        }

        // Login egiteko laguntzailea: erabiltzailea ondo autentikatzen bada, CurrentUsername ezartzen du
        public static string Login(string username, string password)
        {
            string resp = SendRequest("login", username, password);
            if (resp.StartsWith("OK|", StringComparison.OrdinalIgnoreCase))
            {
                CurrentUsername = username;
            }
            return resp;
        }
    }
}
