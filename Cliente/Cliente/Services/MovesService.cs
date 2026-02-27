using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Cliente.Models;

namespace Cliente.Services
{
    public static class MovesService
    {
        // Pokemon motak CSV formatuan jasotzen ditu (adib. "Grass,Poison")
        public static async Task<List<Move>> GetMovesByTypesAsync(string typesCsv)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Zerbitzarira eskaera bidaltzen du mugimenduak lortzeko
                    string resp = ServerService.SendRequest("get_moves_by_type", typesCsv ?? "", "");
                    if (string.IsNullOrWhiteSpace(resp)) return new List<Move>();
                    if (!resp.StartsWith("OK|")) return new List<Move>();

                    // JSON erantzuna deserializatzen du Move objektu zerrenda bihurtzeko
                    string json = resp.Substring(3);
                    var list = JsonSerializer.Deserialize<List<Move>>(json);
                    return list ?? new List<Move>();
                }
                catch
                {
                    // Errore kasuan, zerrenda huts bat bueltatzen du
                    return new List<Move>();
                }
            });
        }
    }
}