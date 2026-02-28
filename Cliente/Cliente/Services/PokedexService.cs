using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Cliente.Models;

namespace Cliente.Services
{
    public static class PokedexService
    {
        // Zerbitzaritik Pokemon zerrenda asinkronoki eskatzen du
        public static async Task<List<Pokemon>> GetPokemonsAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // "get_pokemons" ekintza bidaltzen du zerbitzarira
                    string resp = ServerService.SendRequest("get_pokemons", "", "");
                    if (string.IsNullOrWhiteSpace(resp)) return new List<Pokemon>();

                    int sep = resp.IndexOf('|');
                    if (sep >= 0)
                    {
                        string status = resp.Substring(0, sep);
                        string payload = resp.Substring(sep + 1);
                        if (status == "OK")
                        {
                            // JSON datuak deserializatzen ditu Pokemon objektu bihurtzeko
                            var items = JsonSerializer.Deserialize<List<Pokemon>>(payload);
                            return items ?? new List<Pokemon>();
                        }
                    }
                    // Errore kasuan, zerrenda huts bat bueltatzen du
                    return new List<Pokemon>();
                }
                catch
                {
                    // Salbuespenetan ere, zerrenda huts bat bueltatzen du
                    return new List<Pokemon>();
                }
            });
        }
    }
}