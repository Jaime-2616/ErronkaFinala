using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite; 
using System.Text.Json;

class Program
{
    static TcpListener listener;
    static string connectionString = "Data Source=users.db;";

    // Track connected users in-memory (registered/logged-in users)
    static readonly HashSet<string> connectedUsers = new(StringComparer.OrdinalIgnoreCase);
    static readonly object usersLock = new();

    // Active chat subscribers: username -> ClientInfo
    static readonly Dictionary<string, ClientInfo> subscribers = new(StringComparer.OrdinalIgnoreCase);
    static readonly object subsLock = new();
        
    static void Main()
    {
        InitializeDatabase(); // Crea todas las tablas necesarias y carga JSONs si faltan datos

        listener = new TcpListener(IPAddress.Any, 5000);
        listener.Start();
        Console.WriteLine("Servidor iniciado en el puerto 5000...");

        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            Thread t = new Thread(HandleClient);
            t.Start(client);
        }
    }

    record ClientInfo(string Username, TcpClient Client, NetworkStream Stream);

    // ====================== BASE DE DATOS ======================

    static void InitializeDatabase()
    {
        bool dbExists = File.Exists("users.db");

        if (!dbExists)
        {
            using (var conn = new SqliteConnection(connectionString))
            {
                conn.Open();
            }
        }

        using (var conn = new SqliteConnection(connectionString))
        {
            conn.Open();

            using (var pragma = new SqliteCommand("PRAGMA foreign_keys = ON;", conn))
            {
                pragma.ExecuteNonQuery();
            }

            List<string> tables = new List<string>();

            tables.Add(@"
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL
            );");

            tables.Add(@"
            CREATE TABLE IF NOT EXISTS Pokemon (
                Id INTEGER PRIMARY KEY,
                Name TEXT,
                Type TEXT,
                Base TEXT,
                Thumbnail TEXT,
                HP INTEGER,
                Attack INTEGER,
                Defense INTEGER,
                SpAttack INTEGER,
                SpDefense INTEGER,
                Speed INTEGER
            );");

            tables.Add(@"
            CREATE TABLE IF NOT EXISTS Movimientos (
                Id INTEGER PRIMARY KEY,
                Nombre TEXT,
                Tipo TEXT,
                Categoria TEXT,
                Poder INTEGER,
                Precision INTEGER,
                PP INTEGER
            );");

            tables.Add(@"
            CREATE TABLE IF NOT EXISTS equipos (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                nombre TEXT NOT NULL,
                owner_username TEXT NOT NULL
            );");

            tables.Add(@"
            CREATE TABLE IF NOT EXISTS equipo_pokemon (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                equipo_id INTEGER NOT NULL,
                pokemon_id INTEGER NOT NULL,
                FOREIGN KEY (equipo_id) REFERENCES equipos(id) ON DELETE CASCADE,
                FOREIGN KEY (pokemon_id) REFERENCES Pokemon(Id) ON DELETE CASCADE
            );");

            tables.Add(@"
            CREATE TABLE IF NOT EXISTS equipo_pokemon_movimientos (
                equipo_pokemon_id INTEGER NOT NULL,
                movimiento_id INTEGER NOT NULL,
                slot INTEGER NOT NULL CHECK (slot BETWEEN 1 AND 4),
                PRIMARY KEY (equipo_pokemon_id, slot),
                FOREIGN KEY (equipo_pokemon_id) REFERENCES equipo_pokemon(id) ON DELETE CASCADE,
                FOREIGN KEY (movimiento_id) REFERENCES Movimientos(Id) ON DELETE CASCADE
            );");

            tables.Add(@"
            CREATE TABLE IF NOT EXISTS BattleHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Player1 TEXT NOT NULL,
                Player2 TEXT NOT NULL,
                Winner TEXT,                -- NULL si no hay ganador (doble surrender, etc.)
                EndReason TEXT NOT NULL,    -- 'NORMAL' o 'SURRENDER'
                Player1AliveCount INTEGER NOT NULL,
                Player2AliveCount INTEGER NOT NULL,
                DateUtc TEXT NOT NULL
            );");

            foreach (var sql in tables)
            {
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }

            // Add missing Pokemon stat columns
            string[] statColumns = { "HP", "Attack", "Defense", "SpAttack", "SpDefense", "Speed" };
            foreach (string col in statColumns)
            {
                try
                {
                    using var cmd = new SqliteCommand($"SELECT {col} FROM Pokemon LIMIT 1;", conn);
                    cmd.ExecuteScalar();
                }
                catch
                {
                    using var alter = new SqliteCommand($"ALTER TABLE Pokemon ADD COLUMN {col} INTEGER;", conn);
                    alter.ExecuteNonQuery();
                    Console.WriteLine($"Columna {col} añadida a Pokemon.");
                }
            }

            // NEW: add Points column to Users if missing
            try
            {
                using var cmd = new SqliteCommand("SELECT Points FROM Users LIMIT 1;", conn);
                cmd.ExecuteScalar();
            }
            catch
            {
                using var alter = new SqliteCommand("ALTER TABLE Users ADD COLUMN Points INTEGER NOT NULL DEFAULT 0;", conn);
                alter.ExecuteNonQuery();
                Console.WriteLine("Columna Points añadida a Users.");
            }

            // Check if import needed
            bool needImport = false;
            using (var check = new SqliteCommand("SELECT (SELECT COUNT(1) FROM Pokemon) AS PCount, (SELECT COUNT(1) FROM Movimientos) AS MCount;", conn))
            using (var reader = check.ExecuteReader())
            {
                if (reader.Read())
                {
                    long pCount = reader.GetInt64(0);
                    long mCount = reader.GetInt64(1);
                    Console.WriteLine($"Pokemon en BD: {pCount}, Movimientos en BD: {mCount}");
                    if (pCount == 0 || mCount == 0) needImport = true;
                }
                else
                {
                    needImport = true;
                }
            }

            if (needImport)
            {
                try
                {
                    Console.WriteLine("Importando datos de pokedex y movimientos desde JSON...");
                    ImportPokedexAndMoves(conn);
                    Console.WriteLine("Datos de pokedex y movimientos importados correctamente.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error importing JSON data: " + ex);
                }
            }
            else
            {
                Console.WriteLine("Datos de Pokemon y Movimientos ya existentes. No es necesario importar.");
            }
        }

        Console.WriteLine("Base de datos inicializada correctamente.");
    }

    static void ImportPokedexAndMoves(SqliteConnection conn)
    {
        // RUTAS ABSOLUTAS DURANTE EL DESARROLLO
        // AJUSTA ESTAS DOS LÍNEAS SI CAMBIAS LA UBICACIÓN
        string pokedexPath = @"D:\Bien\Cliente\Cliente\JSON\pokedex.json";
        string movesPath   = @"D:\Bien\Cliente\Cliente\JSON\moves.json";

        Console.WriteLine($"pokedex.json path : {pokedexPath}");
        Console.WriteLine($"moves.json path   : {movesPath}");

        if (!File.Exists(pokedexPath))
            throw new FileNotFoundException("pokedex.json not found", pokedexPath);
        if (!File.Exists(movesPath))
            throw new FileNotFoundException("moves.json not found", movesPath);

        string pokedexText = File.ReadAllText(pokedexPath, Encoding.UTF8);
        string movesText = File.ReadAllText(movesPath, Encoding.UTF8);

        var pokemons = JsonSerializer.Deserialize<JsonElement[]>(pokedexText);
        var moves = JsonSerializer.Deserialize<JsonElement[]>(movesText);

        using (var tran = conn.BeginTransaction())
        {
            // INSERT POKEMON (AHORA CON ESTADÍSTICAS)
            var pCmd = conn.CreateCommand();
            pCmd.Transaction = tran;
            pCmd.CommandText = @"
                INSERT OR REPLACE INTO Pokemon 
                (Id, Name, Type, Base, Thumbnail, HP, Attack, Defense, SpAttack, SpDefense, Speed) 
                VALUES (@id,@name,@type,@base,@thumb,@hp,@atk,@def,@spatk,@spdef,@spd);";
            pCmd.Parameters.Add("@id", SqliteType.Integer);
            pCmd.Parameters.Add("@name", SqliteType.Text);
            pCmd.Parameters.Add("@type", SqliteType.Text);
            pCmd.Parameters.Add("@base", SqliteType.Text);
            pCmd.Parameters.Add("@thumb", SqliteType.Text);
            pCmd.Parameters.Add("@hp", SqliteType.Integer);
            pCmd.Parameters.Add("@atk", SqliteType.Integer);
            pCmd.Parameters.Add("@def", SqliteType.Integer);
            pCmd.Parameters.Add("@spatk", SqliteType.Integer);
            pCmd.Parameters.Add("@spdef", SqliteType.Integer);
            pCmd.Parameters.Add("@spd", SqliteType.Integer);

            int insertedPokemons = 0;

            if (pokemons != null)
            {
                foreach (var item in pokemons)
                {
                    if (!item.TryGetProperty("id", out var idProp)) continue;
                    int id = idProp.GetInt32();

                    string name = "";
                    if (item.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.Object)
                    {
                        if (nameProp.TryGetProperty("english", out var eng) && eng.ValueKind == JsonValueKind.String)
                            name = eng.GetString() ?? "";
                    }

                    string typeCsv = "";
                    if (item.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.Array)
                    {
                        var types = typeProp.EnumerateArray().Select(t => t.GetString() ?? "");
                        typeCsv = string.Join(",", types);
                    }

                    // Guardamos el JSON completo de base por si se quiere usar luego
                    string baseJson = null;
                    JsonElement baseProp;
                    if (item.TryGetProperty("base", out baseProp))
                    {
                        baseJson = baseProp.GetRawText();
                    }

                    // Extraer estadísticas individuales
                    int? hp = null, atk = null, def = null, spatk = null, spdef = null, spd = null;
                    if (baseProp.ValueKind == JsonValueKind.Object)
                    {
                        if (baseProp.TryGetProperty("HP", out var hpProp) && hpProp.ValueKind == JsonValueKind.Number)
                            hp = hpProp.GetInt32();
                        if (baseProp.TryGetProperty("Attack", out var atkProp) && atkProp.ValueKind == JsonValueKind.Number)
                            atk = atkProp.GetInt32();
                        if (baseProp.TryGetProperty("Defense", out var defProp) && defProp.ValueKind == JsonValueKind.Number)
                            def = defProp.GetInt32();
                        if (baseProp.TryGetProperty("Sp. Attack", out var saProp) && saProp.ValueKind == JsonValueKind.Number)
                            spatk = saProp.GetInt32();
                        if (baseProp.TryGetProperty("Sp. Defense", out var sdProp) && sdProp.ValueKind == JsonValueKind.Number)
                            spdef = sdProp.GetInt32();
                        if (baseProp.TryGetProperty("Speed", out var spdProp) && spdProp.ValueKind == JsonValueKind.Number)
                            spd = spdProp.GetInt32();
                    }

                    string thumb = null;
                    if (item.TryGetProperty("image", out var imgProp) && imgProp.ValueKind == JsonValueKind.Object)
                    {
                        if (imgProp.TryGetProperty("thumbnail", out var tprop) && tprop.ValueKind == JsonValueKind.String)
                            thumb = tprop.GetString();
                    }

                    pCmd.Parameters["@id"].Value = id;
                    pCmd.Parameters["@name"].Value = name ?? "";
                    pCmd.Parameters["@type"].Value = typeCsv ?? "";
                    pCmd.Parameters["@base"].Value = baseJson ?? "";
                    pCmd.Parameters["@thumb"].Value = thumb ?? "";
                    pCmd.Parameters["@hp"].Value = hp.HasValue ? (object)hp.Value : DBNull.Value;
                    pCmd.Parameters["@atk"].Value = atk.HasValue ? (object)atk.Value : DBNull.Value;
                    pCmd.Parameters["@def"].Value = def.HasValue ? (object)def.Value : DBNull.Value;
                    pCmd.Parameters["@spatk"].Value = spatk.HasValue ? (object)spatk.Value : DBNull.Value;
                    pCmd.Parameters["@spdef"].Value = spdef.HasValue ? (object)spdef.Value : DBNull.Value;
                    pCmd.Parameters["@spd"].Value = spd.HasValue ? (object)spd.Value : DBNull.Value;

                    pCmd.ExecuteNonQuery();
                    insertedPokemons++;
                }
            }

            Console.WriteLine($"Pokmon insertados: {insertedPokemons}");

            // INSERT MOVIMIENTOS
            var mCmd = conn.CreateCommand();
            mCmd.Transaction = tran;
            mCmd.CommandText = @"INSERT OR REPLACE INTO Movimientos (Id, Nombre, Tipo, Categoria, Poder, Precision, PP) 
                                 VALUES (@id,@nombre,@tipo,@categoria,@poder,@precision,@pp);";
            mCmd.Parameters.Add("@id", SqliteType.Integer);
            mCmd.Parameters.Add("@nombre", SqliteType.Text);
            mCmd.Parameters.Add("@tipo", SqliteType.Text);
            mCmd.Parameters.Add("@categoria", SqliteType.Text);
            mCmd.Parameters.Add("@poder", SqliteType.Integer);
            mCmd.Parameters.Add("@precision", SqliteType.Integer);
            mCmd.Parameters.Add("@pp", SqliteType.Integer);

            int insertedMoves = 0;

            if (moves != null)
            {
                foreach (var mv in moves)
                {
                    if (!mv.TryGetProperty("id", out var idProp)) continue;
                    int id = idProp.GetInt32();

                    string nombre = "";
                    if (mv.TryGetProperty("ename", out var en) && en.ValueKind == JsonValueKind.String)
                        nombre = en.GetString() ?? "";
                    else if (mv.TryGetProperty("cname", out var cn) && cn.ValueKind == JsonValueKind.String)
                        nombre = cn.GetString() ?? "";

                    string tipo = mv.TryGetProperty("type", out var typeP) && typeP.ValueKind == JsonValueKind.String ? typeP.GetString() ?? "" : "";
                    string categoria = mv.TryGetProperty("category", out var catP) && catP.ValueKind == JsonValueKind.String ? catP.GetString() ?? "" : "";

                    int? poder = null;
                    if (mv.TryGetProperty("power", out var powerP) && powerP.ValueKind == JsonValueKind.Number)
                        poder = powerP.GetInt32();

                    int? precision = null;
                    if (mv.TryGetProperty("accuracy", out var accP) && accP.ValueKind == JsonValueKind.Number)
                        precision = accP.GetInt32();

                    int? pp = null;
                    if (mv.TryGetProperty("pp", out var ppP) && ppP.ValueKind == JsonValueKind.Number)
                        pp = ppP.GetInt32();

                    mCmd.Parameters["@id"].Value = id;
                    mCmd.Parameters["@nombre"].Value = nombre ?? "";
                    mCmd.Parameters["@tipo"].Value = tipo ?? "";
                    mCmd.Parameters["@categoria"].Value = categoria ?? "";
                    mCmd.Parameters["@poder"].Value = poder.HasValue ? (object)poder.Value : DBNull.Value;
                    mCmd.Parameters["@precision"].Value = precision.HasValue ? (object)precision.Value : DBNull.Value;
                    mCmd.Parameters["@pp"].Value = pp.HasValue ? (object)pp.Value : DBNull.Value;

                    mCmd.ExecuteNonQuery();
                    insertedMoves++;
                }
            }

            Console.WriteLine($"Movimientos insertados: {insertedMoves}");

            tran.Commit();
        }
    }

    // ====================== HASH DE CONTRASEÑAS ======================

    static string HashPassword(string password)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(password);
            byte[] hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }

    // ====================== MANTENIMIENTO DE USUARIOS ======================

    static string RegisterUser(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return "ERROR|Nombre o contraseña inválidos.";

        string passwordHash = HashPassword(password);

        try
        {
            using (var conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                using (var check = new SqliteCommand("SELECT COUNT(1) FROM Users WHERE Username = @u", conn))
                {
                    check.Parameters.AddWithValue("@u", username);
                    long count = (long)check.ExecuteScalar();
                    if (count > 0)
                        return "ERROR|El usuario ya existe.";
                }

                using (var cmd = new SqliteCommand("INSERT INTO Users (Username, PasswordHash, Points) VALUES (@u, @p, 0)", conn))
                {
                    cmd.Parameters.AddWithValue("@u", username);
                    cmd.Parameters.AddWithValue("@p", passwordHash);
                    cmd.ExecuteNonQuery();
                }
            }

            return "OK|Registro exitoso.";
        }
        catch (Exception ex)
        {
            return "ERROR|Error en registro: " + ex.Message;
        }
    }

    static string LoginUser(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return "ERROR|Nombre o contraseña inválidos.";

        string passwordHash = HashPassword(password);

        try
        {
            using (var conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                using (var cmd = new SqliteCommand("SELECT PasswordHash FROM Users WHERE Username = @u", conn))
                {
                    cmd.Parameters.AddWithValue("@u", username);
                    var result = cmd.ExecuteScalar();
                    if (result == null)
                        return "ERROR|Usuario no encontrado.";

                    string storedHash = result.ToString();
                    if (storedHash != passwordHash)
                        return "ERROR|Contraseña incorrecta.";

                    lock (usersLock)
                    {
                        if (!connectedUsers.Contains(username))
                        {
                            connectedUsers.Add(username);
                            Console.WriteLine($"{username} conectado.");
                        }
                    }

                    return "OK|Login exitoso.";
                }
            }
        }
        catch (Exception ex)
        {
            return "ERROR|Error en login: " + ex.Message;
        }
    }

    // ====================== MANEJO DE CLIENTES (persistente y one-shot) ======================

    static void HandleClient(object obj)
    {
        var client = obj as TcpClient;
        if (client == null) return;

        var remote = client.Client.RemoteEndPoint?.ToString() ?? "desconocido";

        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var buffer = new byte[4096];
                var sb = new StringBuilder();

                // Read loop - supports one-shot requests (client closes) and persistent subscribe connections
                while (true)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break; // client closed connection

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, read));

                    // Process complete lines (messages terminated by '\n' or single message without newline)
                    string content = sb.ToString();
                    int newlineIndex;
                    while ((newlineIndex = content.IndexOf('\n')) >= 0)
                    {
                        string line = content.Substring(0, newlineIndex).Trim('\r');
                        content = content.Substring(newlineIndex + 1);
                        ProcessMessage(line, stream, client);
                    }

                    // If no newline but client closed further reads, handle remaining content next loop or when closed.
                    sb.Clear();
                    sb.Append(content);
                }

                // connection closing - cleanup if this client was subscribed
                CleanupSubscriberByStream(stream);
            }
        }
        catch (IOException)
        {
            // Avoid calling client.GetStream() here because client may already be disposed.
            CleanupSubscriberByClient(client);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Client handler error: " + ex.Message);
            // Attempt best-effort cleanup
            try { CleanupSubscriberByClient(client); } catch { }
        }
    }

    static void ProcessMessage(string message, NetworkStream stream, TcpClient tcpClient)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        // LOG PARA VER QUÉ LLEGA
        Console.WriteLine("Mensaje recibido: [" + message + "]");

        string[] parts = message.Split('|', StringSplitOptions.None);
        string action = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";

        string response = "ERROR|Petición inválida.";

        try
        {
            if (action == "register" && parts.Length >= 3)
            {
                response = RegisterUser(parts[1], parts[2]);
                WriteResponse(stream, response);
            }
            else if (action == "login" && parts.Length >= 3)
            {
                response = LoginUser(parts[1], parts[2]);
                WriteResponse(stream, response);
            }
            else if (action == "logout" && parts.Length >= 2)
            {
                string username = parts[1];
                lock (usersLock)
                {
                    if (connectedUsers.Remove(username))
                    {
                        Console.WriteLine($"{username} desconectado.");
                    }
                }
                // También eliminar suscripción si existe
                lock (subsLock)
                {
                    if (subscribers.Remove(username, out var info))
                    {
                        try { info.Stream.Close(); info.Client.Close(); } catch { }
                        Console.WriteLine($"{username} unsubscribed (logout).");
                    }
                }
                response = "OK|Logout exitoso.";
                WriteResponse(stream, response);
            }
            else if (action == "get_players")
            {
                // optional requester passed as second param: get_players|requester
                string requester = parts.Length >= 2 ? parts[1] : null;
                string csv;
                lock (usersLock)
                {
                    var list = connectedUsers.AsEnumerable();
                    if (!string.IsNullOrWhiteSpace(requester))
                        list = list.Where(u => !string.Equals(u, requester, StringComparison.OrdinalIgnoreCase));
                    csv = string.Join(",", list);
                }
                response = "OK|" + csv;
                WriteResponse(stream, response);
            }
            else if (action == "get_players_with_points")
            {
                // optional requester passed as second param: get_players_with_points|requester
                string requester = parts.Length >= 2 ? parts[1] : null;

                string[] users;
                lock (usersLock)
                {
                    var list = connectedUsers.AsEnumerable();
                    if (!string.IsNullOrWhiteSpace(requester))
                        list = list.Where(u => !string.Equals(u, requester, StringComparison.OrdinalIgnoreCase));
                    users = list.ToArray();
                }

                try
                {
                    using var conn = new SqliteConnection(connectionString);
                    conn.Open();

                    var results = new List<string>(users.Length);

                    using var cmd = new SqliteCommand("SELECT COALESCE(Points,0) FROM Users WHERE Username = @u;", conn);
                    var pUser = cmd.Parameters.Add("@u", SqliteType.Text);

                    foreach (var u in users)
                    {
                        pUser.Value = u;
                        int pts = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                        results.Add($"{u}:{pts}");
                    }

                    WriteResponse(stream, "OK|" + string.Join(",", results));
                }
                catch (Exception ex)
                {
                    WriteResponse(stream, "ERROR|DB error: " + ex.Message);
                }
            }
            else if (action == "get_pokemons")
            {
                try
                {
                    using (var conn = new SqliteConnection(connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqliteCommand(
                            @"SELECT Id, Name, Thumbnail, HP, Attack, Defense, SpAttack, SpDefense, Speed 
                              FROM Pokemon 
                              ORDER BY Id;", conn))
                        using (var reader = cmd.ExecuteReader())
                        {
                            var list = new List<object>();
                            while (reader.Read())
                            {
                                int id = reader.GetInt32(0);
                                string name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                string thumb = reader.IsDBNull(2) ? "" : reader.GetString(2);

                                int? hp       = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
                                int? attack   = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
                                int? defense  = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
                                int? spAttack = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6);
                                int? spDefense= reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7);
                                int? speed    = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8);

                                list.Add(new 
                                { 
                                    Id = id, 
                                    Name = name, 
                                    Thumbnail = thumb,
                                    HP = hp,
                                    Attack = attack,
                                    Defense = defense,
                                    SpAttack = spAttack,
                                    SpDefense = spDefense,
                                    Speed = speed
                                });
                            }
                            string json = JsonSerializer.Serialize(list);
                            WriteResponse(stream, "OK|" + json);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteResponse(stream, "ERROR|DB error: " + ex.Message);
                }
            }
            else if (action == "get_teams" && parts.Length >= 2)
            {
                // get_teams|username
                string username = parts[1];
                try
                {
                    using (var conn = new SqliteConnection(connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqliteCommand(
                            "SELECT nombre FROM equipos WHERE owner_username = @u ORDER BY id;", conn))
                        {
                            cmd.Parameters.AddWithValue("@u", username ?? string.Empty);
                            using (var reader = cmd.ExecuteReader())
                            {
                                var list = new List<string>();
                                while (reader.Read())
                                {
                                    string name = reader.IsDBNull(0) ? "" : reader.GetString(0);
                                    list.Add(name);
                                }
                                string json = JsonSerializer.Serialize(list);
                                WriteResponse(stream, "OK|" + json);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteResponse(stream, "ERROR|DB error: " + ex.Message);
                }
            }
            else if (action == "get_team" && parts.Length >= 2)
            {
                // Devuelve JSON de pokémon del equipo, con estadísticas completas y movimientos (si existen)
                string teamName = parts[1];
                try
                {
                    using (var conn = new SqliteConnection(connectionString))
                    {
                        conn.Open();

                        // 1) Obtener id del equipo
                        long equipoId;
                        using (var cmdTeam = new SqliteCommand("SELECT id FROM equipos WHERE nombre = @n;", conn))
                        {
                            cmdTeam.Parameters.AddWithValue("@n", teamName ?? "");
                            var result = cmdTeam.ExecuteScalar();
                            if (result == null)
                            {
                                WriteResponse(stream, "ERROR|Equipo no encontrado.");
                                return;
                            }
                            equipoId = (long)result;
                        }

                        // 2) Cargar los equipo_pokemon del equipo (para luego mapear movimientos)
                        var equipoPokemonList = new List<(long EpId, int PokemonId)>();
                        using (var cmdEp = new SqliteCommand(
                            "SELECT id, pokemon_id FROM equipo_pokemon WHERE equipo_id = @e ORDER BY id;", conn))
                        {
                            cmdEp.Parameters.AddWithValue("@e", equipoId);
                            using (var reader = cmdEp.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    long epId = reader.GetInt64(0);
                                    int pokemonId = reader.GetInt32(1);
                                    equipoPokemonList.Add((epId, pokemonId));
                                }
                            }
                        }

                        // 3) Cargar movimientos de todos esos equipo_pokemon en un diccionario:
                        //    equipo_pokemon_id -> slot -> (movId, movNombre)
                        var movesByEp = new Dictionary<long, Dictionary<int, (int movId, string movName, int? movPower, string movCategoria, string movTipo)>>();

                        if (equipoPokemonList.Count > 0)
                        {
                            string inClause = string.Join(",", equipoPokemonList.Select(ep => ep.EpId.ToString()));
                            string sqlMovs = $@"
        SELECT epm.equipo_pokemon_id, epm.slot, m.Id, m.Nombre, m.Poder, m.Categoria, m.Tipo
        FROM equipo_pokemon_movimientos epm
        JOIN Movimientos m ON m.Id = epm.movimiento_id
        WHERE epm.equipo_pokemon_id IN ({inClause});";

                            using (var cmdMovs = new SqliteCommand(sqlMovs, conn))
                            using (var reader = cmdMovs.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    long epId        = reader.GetInt64(0);
                                    int  slot        = reader.GetInt32(1);
                                    int  movId       = reader.GetInt32(2);
                                    string movName   = reader.IsDBNull(3) ? "" : reader.GetString(3);
                                    int? movPower    = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
                                    string movCat = reader.IsDBNull(5) ? "" : reader.GetString(5);
                                    string movTipo = reader.IsDBNull(6) ? "" : reader.GetString(6);

                                    if (!movesByEp.TryGetValue(epId, out var dict))
                                    {
                                        dict = new Dictionary<int, (int, string, int?, string, string)>();
                                        movesByEp[epId] = dict;
                                    }

                                    dict[slot] = (movId, movName, movPower, movCat, movTipo);
                                }
                            }
                        }

                        // 4) Cargar datos de los pokémon del equipo con stats y tipo.
                        //    Usamos la lista equipoPokemonList para mantener el orden.
                        var list = new List<object>();

                        foreach (var (epId, pokemonId) in equipoPokemonList)
                        {
                            using (var cmdPoke = new SqliteCommand(@"
                                SELECT 
                                    Id, 
                                    Name, 
                                    Thumbnail,
                                    Type,
                                    HP,
                                    Attack,
                                    Defense,
                                    SpAttack,
                                    SpDefense,
                                    Speed
                                FROM Pokemon
                                WHERE Id = @id;", conn))
                            {
                                cmdPoke.Parameters.AddWithValue("@id", pokemonId);

                                using (var reader = cmdPoke.ExecuteReader())
                                {
                                    if (!reader.Read())
                                        continue;

                                    int id        = reader.GetInt32(0);
                                    string name   = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                    string thumb  = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                    string type   = reader.IsDBNull(3) ? "" : reader.GetString(3);

                                    int? hp       = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
                                    int? attack   = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
                                    int? defense  = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6);
                                    int? spAttack = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7);
                                    int? spDefense= reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8);
                                    int? speed    = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9);

                                    // Recuperar movimientos, si existen, para este equipo_pokemon
                                    int? move1Id = null, move2Id = null, move3Id = null, move4Id = null;
                                    string  move1 = "", move2 = "", move3 = "", move4 = "";
                                    int?    move1Power = null, move2Power = null, move3Power = null, move4Power = null;
                                    string  move1Cat = "", move2Cat = "", move3Cat = "", move4Cat = "";
                                    string  move1Type = "", move2Type = "", move3Type = "", move4Type = "";

                                    if (movesByEp.TryGetValue(epId, out var slots))
                                    {
                                        if (slots.TryGetValue(1, out var m1))
                                        {
                                            move1Id    = m1.movId;
                                            move1      = m1.movName;
                                            move1Power = m1.movPower;
                                            move1Cat   = m1.movCategoria;
                                            move1Type  = m1.movTipo;
                                        }
                                        if (slots.TryGetValue(2, out var m2))
                                        {
                                            move2Id    = m2.movId;
                                            move2      = m2.movName;
                                            move2Power = m2.movPower;
                                            move2Cat   = m2.movCategoria;
                                            move2Type  = m2.movTipo;
                                        }
                                        if (slots.TryGetValue(3, out var m3))
                                        {
                                            move3Id    = m3.movId;
                                            move3      = m3.movName;
                                            move3Power = m3.movPower;
                                            move3Cat   = m3.movCategoria;
                                            move3Type  = m3.movTipo;
                                        }
                                        if (slots.TryGetValue(4, out var m4))
                                        {
                                            move4Id    = m4.movId;
                                            move4      = m4.movName;
                                            move4Power = m4.movPower;
                                            move4Cat   = m4.movCategoria;
                                            move4Type  = m4.movTipo;
                                        }
                                    }

                                    list.Add(new
                                    {
                                        Id = id,
                                        Name = name,
                                        Thumbnail = thumb,
                                        Type = type,
                                        HP = hp,
                                        Attack = attack,
                                        Defense = defense,
                                        SpAttack = spAttack,
                                        SpDefense = spDefense,
                                        Speed = speed,
                                        Move1Id = move1Id,
                                        Move2Id = move2Id,
                                        Move3Id = move3Id,
                                        Move4Id = move4Id,
                                        Move1 = move1,
                                        Move2 = move2,
                                        Move3 = move3,
                                        Move4 = move4,
                                        Move1Power = move1Power,
                                        Move2Power = move2Power,
                                        Move3Power = move3Power,
                                        Move4Power = move4Power,
                                        Move1Category = move1Cat,
                                        Move2Category = move2Cat,
                                        Move3Category = move3Cat,
                                        Move4Category = move4Cat,
                                        Move1Type = move1Type,
                                        Move2Type = move2Type,
                                        Move3Type = move3Type,
                                        Move4Type = move4Type
                                    });
                                }
                            }
                        }

                        string jsonOut = JsonSerializer.Serialize(list);
                        Console.WriteLine("get_team json: " + jsonOut);
                        WriteResponse(stream, "OK|" + jsonOut);
                    }
                }
                catch (Exception ex)
                {
                    WriteResponse(stream, "ERROR|DB error: " + ex.Message);
                }
            }
            else if (action == "create_team" && parts.Length >= 4)
            {
                // create_team|username|teamName|jsonIds
                string username = parts[1];
                string teamName = parts[2];
                string jsonIds = parts[3];

                try
                {
                    int[] ids = JsonSerializer.Deserialize<int[]>(jsonIds) ?? Array.Empty<int>();
                    string res = CreateTeam(username, teamName, ids);
                    WriteResponse(stream, res);
                }
                catch (Exception ex)
                {
                    WriteResponse(stream, "ERROR|Formato de datos inválido: " + ex.Message);
                }
            }
            // ===== NUEVO: eliminar equipo =====
            else if (action == "delete_team" && parts.Length >= 3)
            {
                // delete_team|username|teamName
                string username = parts[1];
                string teamName = parts[2];

                try
                {
                    using (var conn = new SqliteConnection(connectionString))
                    {
                        conn.Open();

                        using (var cmd = new SqliteCommand(
                            "DELETE FROM equipos WHERE nombre = @n AND owner_username = @u;", conn))
                        {
                            cmd.Parameters.AddWithValue("@n", teamName ?? string.Empty);
                            cmd.Parameters.AddWithValue("@u", username ?? string.Empty);

                            int affected = cmd.ExecuteNonQuery();
                            if (affected > 0)
                            {
                                // Gracias a ON DELETE CASCADE se eliminan también equipo_pokemon y equipo_pokemon_movimientos
                                WriteResponse(stream, "OK|Equipo eliminado.");
                            }
                            else
                            {
                                WriteResponse(stream, "ERROR|No se encontró el equipo o no eres el propietario.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteResponse(stream, "ERROR|DB error al eliminar equipo: " + ex.Message);
                }
            }
            else if (action == "subscribe" && parts.Length >= 2)
            {
                // Client wants a persistent chat connection
                string username = parts[1];
                Console.WriteLine($"Subscribe request: {username} from {tcpClient.Client.RemoteEndPoint}");
                lock (subsLock)
                {
                    // If an existing subscriber with same username exists, remove it first (clean up)
                    if (subscribers.TryGetValue(username, out var old))
                    {
                        try { old.Stream.Close(); old.Client.Close(); } catch { }
                        subscribers.Remove(username);
                    }

                    subscribers[username] = new ClientInfo(username, tcpClient, stream);
                }

                // Ensure connectedUsers contains username as well
                lock (usersLock)
                {
                    if (!connectedUsers.Contains(username))
                    {
                        connectedUsers.Add(username);
                        Console.WriteLine($"{username} conectado.");
                    }
                }

                WriteResponse(stream, "OK|Subscribed");
            }
            else if (action == "chat")
            {
                // chat|message (sender is determined by subscribers map)
                // Prefer matching the TcpClient first (more robust), fall back to stream match
                string sender = GetUsernameByClient(tcpClient) ?? GetUsernameByStream(stream);
                string text = parts.Length >= 2 ? parts[1] : "";
                if (string.IsNullOrEmpty(sender))
                {
                    // If we don't know sender, ignore or respond error
                    Console.WriteLine("Chat from unknown sender (unmatched client/stream).");
                    WriteResponse(stream, "ERROR|No suscrito para chat.");
                    return;
                }

                // Broadcast to all subscribers: "MSG|sender|text\n"
                Console.WriteLine($"Broadcasting from [{sender}]: {text}");
                BroadcastMessage(sender, text);
                // optional ACK
                //WriteResponse(stream, "OK|Sent");
            }
            else if (action == "get_points" && parts.Length >= 2)
            {
                string username = parts[1];

                try
                {
                    using var conn = new SqliteConnection(connectionString);
                    conn.Open();

                    using var cmd = new SqliteCommand("SELECT COALESCE(Points,0) FROM Users WHERE Username = @u;", conn);
                    cmd.Parameters.AddWithValue("@u", username);

                    int pts = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                    WriteResponse(stream, "OK|" + pts);
                }
                catch (Exception ex)
                {
                    WriteResponse(stream, "ERROR|DB error: " + ex.Message);
                }
            }
            else if (action == "get_moves_by_type")
            {
                // parts[1] = CSV de tipos ("Grass,Poison") o vacío para todos
                string typesCsv = parts.Length >= 2 ? parts[1] : null;

                try
                {
                    using (var conn = new SqliteConnection(connectionString))
                    {
                        conn.Open();

                        SqliteCommand cmd;
                        if (string.IsNullOrWhiteSpace(typesCsv))
                        {
                            cmd = new SqliteCommand(
                                @"SELECT Id, Nombre, Tipo, Categoria, Poder, Precision, PP 
                                  FROM Movimientos
                                  ORDER BY Nombre;", conn);
                        }
                        else
                        {
                            // Filtramos por tipo; si hay varios, usamos IN (...)
                            var types = typesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (types.Length == 0)
                            {
                                cmd = new SqliteCommand(
                                    @"SELECT Id, Nombre, Tipo, Categoria, Poder, Precision, PP 
                                      FROM Movimientos
                                      ORDER BY Nombre;", conn);
                            }
                            else
                            {
                                // Construimos parámetros @t0,@t1,...
                                var where = string.Join(",", types.Select((t, i) => $"@t{i}"));
                                cmd = new SqliteCommand(
                                    $@"SELECT Id, Nombre, Tipo, Categoria, Poder, Precision, PP 
                                       FROM Movimientos
                                       WHERE Tipo IN ({where})
                                       ORDER BY Nombre;", conn);
                                for (int i = 0; i < types.Length; i++)
                                {
                                    cmd.Parameters.AddWithValue($"@t{i}", types[i]);
                                }
                            }
                        }

                        using (cmd)
                        using (var reader = cmd.ExecuteReader())
                        {
                            var list = new List<object>();
                            while (reader.Read())
                            {
                                int id = reader.GetInt32(0);
                                string nombre = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                string tipo = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                string categoria = reader.IsDBNull(3) ? "" : reader.GetString(3);
                                int? poder = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
                                int? precision = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
                                int? pp = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6);

                                list.Add(new
                                {
                                    Id = id,
                                    Nombre = nombre,
                                    Tipo = tipo,
                                    Categoria = categoria,
                                    Poder = poder,
                                    Precision = precision,
                                    PP = pp
                                });
                            }

                            string json = JsonSerializer.Serialize(list);
                            WriteResponse(stream, "OK|" + json);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteResponse(stream, "ERROR|DB error: " + ex.Message);
                }
            }
            else if (action == "save_team_moves" && parts.Length >= 3)
{
    string teamName = parts[1];
    string json = parts[2];

    try
    {
        using (var conn = new SqliteConnection(connectionString))
        {
            conn.Open();

            // Obtener id del equipo
            long equipoId;
            using (var cmd = new SqliteCommand("SELECT id FROM equipos WHERE nombre = @n;", conn))
            {
                cmd.Parameters.AddWithValue("@n", teamName ?? "");
                var result = cmd.ExecuteScalar();
                if (result == null)
                {
                    WriteResponse(stream, "ERROR|Equipo no encontrado.");
                    return;
                }
                equipoId = (long)(result);
            }

            // ⚠️ QUITAR opciones de comentarios: JsonDocument EZ du iruzkinik onartzen
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true
                // CommentHandling ez da hemen erabili behar
            });
            var root = doc.RootElement;
            if (!root.TryGetProperty("Pokemons", out var pokesElem) || 
                pokesElem.ValueKind != JsonValueKind.Array)
            {
                WriteResponse(stream, "ERROR|Formato JSON inválido.");
                return;
            }

            using (var tran = conn.BeginTransaction())
            {
                // Mapa pokemon_id -> equipo_pokemon.id
                var mapEquipoPokemon = new Dictionary<int, long>();

                using (var cmd = new SqliteCommand(
                    "SELECT id, pokemon_id FROM equipo_pokemon WHERE equipo_id = @e;", conn, tran))
                {
                    cmd.Parameters.AddWithValue("@e", equipoId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            long epId = reader.GetInt64(0);
                            int pokemonId = reader.GetInt32(1);
                            mapEquipoPokemon[pokemonId] = epId;
                        }
                    }
                }

                // Borrar movimientos existentes
                if (mapEquipoPokemon.Count > 0)
                {
                    using var delCmd = new SqliteCommand(
                        "DELETE FROM equipo_pokemon_movimientos WHERE equipo_pokemon_id = @epId;", conn, tran);
                    var pEpId = delCmd.Parameters.Add("@epId", SqliteType.Integer);

                    foreach (var epId in mapEquipoPokemon.Values)
                    {
                        pEpId.Value = epId;
                        delCmd.ExecuteNonQuery();
                    }
                }

                // Insertar nuevos movimientos
                using (var insCmd = new SqliteCommand(
                    @"INSERT INTO equipo_pokemon_movimientos (equipo_pokemon_id, movimiento_id, slot)
                      VALUES (@epId, @movId, @slot);", conn, tran))
                {
                    var pEpId = insCmd.Parameters.Add("@epId", SqliteType.Integer);
                    var pMovId = insCmd.Parameters.Add("@movId", SqliteType.Integer);
                    var pSlot = insCmd.Parameters.Add("@slot", SqliteType.Integer);

                    foreach (var p in pokesElem.EnumerateArray())
                    {
                        if (!p.TryGetProperty("PokemonId", out var pidElem) || 
                            pidElem.ValueKind != JsonValueKind.Number)
                            continue;

                        int pokemonId = pidElem.GetInt32();
                        if (!mapEquipoPokemon.TryGetValue(pokemonId, out long epId))
                            continue;

                        for (int slot = 1; slot <= 4; slot++)
                        {
                            string propName = $"Move{slot}Id";
                            if (!p.TryGetProperty(propName, out var mvElem) || 
                                mvElem.ValueKind != JsonValueKind.Number)
                                continue;

                            int movId = mvElem.GetInt32();

                            pEpId.Value = epId;
                            pMovId.Value = movId;
                            pSlot.Value = slot;

                            insCmd.ExecuteNonQuery();
                        }
                    }
                }

                tran.Commit();
            }
        }

        WriteResponse(stream, "OK|Movimientos guardados.");
    }
    catch (Exception ex)
    {
        WriteResponse(stream, "ERROR|No se pudieron guardar los movimientos: " + ex.Message);
    }
}
            else if (action == "challenge" && parts.Length >= 3)
            {
                // challenge|fromUser|toUser
                string fromUser = parts[1];
                string toUser = parts[2];

                Console.WriteLine($"Challenge from {fromUser} to {toUser}");

                lock (subsLock)
                {
                    if (!subscribers.ContainsKey(toUser))
                    {
                        // El retado no está suscrito / online
                        WriteResponse(stream, "ERROR|El jugador no está disponible.");
                        return;
                    }
                }

                // Notificar al retado
                // Formato: CHALLENGE|fromUser
                SendPrivateMessage(toUser, $"CHALLENGE|{fromUser}");

                // Opcional: ACK al retador
                WriteResponse(stream, "OK|Reto enviado.");
            }
            else if (action == "challenge_response" && parts.Length >= 4)
            {
                // challenge_response|responder|challenger|ACCEPT/REJECT
                string responder = parts[1];
                string challenger = parts[2];
                string decision = parts[3].ToUpperInvariant();

                Console.WriteLine($"Challenge response from {responder} to {challenger}: {decision}");

                if (decision == "ACCEPT")
                {
                    // Notificar a ambos que comienza combate
                    // Formato: BATTLE_START|challenger|responder
                    string payload = $"BATTLE_START|{challenger}|{responder}";
                    SendPrivateMessage(challenger, payload);
                    SendPrivateMessage(responder, payload);
                }
                else
                {
                    // Notificar solo al retador que fue rechazado
                    // Formato: CHALLENGE_REJECTED|responder
                    SendPrivateMessage(challenger, $"CHALLENGE_REJECTED|{responder}");
                }

                WriteResponse(stream, "OK|Respuesta de reto procesada.");
            }
            else if (action == "team_selected" && parts.Length >= 4)
            {
                // team_selected|player1|player2|teamName
                string p1 = parts[1];
                string p2 = parts[2];
                string teamName = parts[3];

                string key = MakeBattleKey(p1, p2);
                BattleState st;

                lock (battlesLock)
                {
                    if (!battles.TryGetValue(key, out st))
                    {
                        var k = key.Split('|', 2);
                        st = new BattleState(k[0], k[1], null, null);
                    }

                    if (string.Equals(p1, st.PlayerA, StringComparison.OrdinalIgnoreCase))
                        st = st with { TeamA = teamName };
                    else if (string.Equals(p1, st.PlayerB, StringComparison.OrdinalIgnoreCase))
                        st = st with { TeamB = teamName };
                    else
                    {
                        WriteResponse(stream, "ERROR|Jugador inválido para este combate.");
                        return;
                    }

                    battles[key] = st;
                }

                // cuando ambos han elegido, avisar a cada uno del equipo del rival
                if (!string.IsNullOrWhiteSpace(st.TeamA) && !string.IsNullOrWhiteSpace(st.TeamB))
                {
                    SendPrivateMessage(st.PlayerA, $"RIVAL_TEAM|{st.PlayerB}|{st.TeamB}");
                    SendPrivateMessage(st.PlayerB, $"RIVAL_TEAM|{st.PlayerA}|{st.TeamA}");
                }

                WriteResponse(stream, "OK|Team seleccionado.");
            }
            else if (action == "surrender" && parts.Length >= 3)
            {
                // surrender|fromUser|toUser|aliveFrom|aliveTo
                string fromUser = parts[1];
                string toUser = parts[2];

                int aliveFrom = parts.Length >= 4 && int.TryParse(parts[3], out var af) ? af : 0;
                int aliveTo   = parts.Length >= 5 && int.TryParse(parts[4], out var at) ? at : 0;

                Console.WriteLine($"Surrender from {fromUser} to {toUser}");

                SendPrivateMessage(toUser, $"SURRENDER|{fromUser}");

                try
                {
                    using var conn = new SqliteConnection(connectionString);
                    conn.Open();

                    using var tran = conn.BeginTransaction();

                    // Registrar en BattleHistory con ganador claro: el rival
                    using (var histCmd = new SqliteCommand(@"
                        INSERT INTO BattleHistory
                            (Player1, Player2, Winner, EndReason, Player1AliveCount, Player2AliveCount, DateUtc)
                        VALUES (@p1, @p2, @winner, 'SURRENDER', @a1, @a2, @date);", conn, tran))
                    {
                        histCmd.Parameters.AddWithValue("@p1", fromUser);
                        histCmd.Parameters.AddWithValue("@p2", toUser);
                        histCmd.Parameters.AddWithValue("@winner", toUser); // ganador = el que NO se rinde
                        histCmd.Parameters.AddWithValue("@a1", aliveFrom);
                        histCmd.Parameters.AddWithValue("@a2", aliveTo);
                        histCmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
                        histCmd.ExecuteNonQuery();
                    }

                    tran.Commit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[SURRENDER] DB error: " + ex.Message);
                }

                WriteResponse(stream, "OK|Surrender enviado.");
            }
            else if (action == "move_selected" && parts.Length >= 4)
            {
                // move_selected|fromUser|toUser|slot
                string fromUser = parts[1];
                string toUser = parts[2];

                if (!int.TryParse(parts[3], out int slot) || slot < 1 || slot > 4)
                {
                    WriteResponse(stream, "ERROR|Slot inválido.");
                    return;
                }

                // Forward to rival on the persistent connection
                SendPrivateMessage(toUser, $"RIVAL_MOVE|{fromUser}|{slot}");

                WriteResponse(stream, "OK|Move enviado.");
            }
            else if (action == "battle_end" && parts.Length >= 4)
            {
                // battle_end|winner|player1|player2|alive1|alive2
                string winner = parts[1];
                string p1 = parts[2];
                string p2 = parts[3];

                int alive1 = parts.Length >= 5 && int.TryParse(parts[4], out var a1) ? a1 : 0;
                int alive2 = parts.Length >= 6 && int.TryParse(parts[5], out var a2) ? a2 : 0;

                string loser = string.Equals(winner, p1, StringComparison.OrdinalIgnoreCase) ? p2 :
                               string.Equals(winner, p2, StringComparison.OrdinalIgnoreCase) ? p1 : null;

                Console.WriteLine($"[BATTLE_END] Winner: {winner} | Match: {p1} vs {p2}");

                if (string.IsNullOrWhiteSpace(loser))
                {
                    Console.WriteLine("[BATTLE_END] ERROR: winner no coincide con player1/player2.");
                    WriteResponse(stream, "ERROR|Winner inválido.");
                    return;
                }

                try
                {
                    using (var conn = new SqliteConnection(connectionString))
                    {
                        conn.Open();
                        using var tran = conn.BeginTransaction();

                        // calcular cuantos vivos tiene el ganador
                        int winnerAlive = string.Equals(winner, p1, StringComparison.OrdinalIgnoreCase) ? alive1 : alive2;

                        int winnerDelta;
                        if (winnerAlive == 3)
                            winnerDelta = 30;
                        else if (winnerAlive == 4)
                            winnerDelta = 40;
                        else if (winnerAlive == 5)
                            winnerDelta = 50;
                        else if (winnerAlive >= 6)
                            winnerDelta = 60;
                        else
                            winnerDelta = 20;

                        const int loserDelta = -30;

                        using (var cmdWin = new SqliteCommand(
                            "UPDATE Users SET Points = COALESCE(Points,0) + @delta WHERE Username = @u;", conn, tran))
                        {
                            cmdWin.Parameters.AddWithValue("@u", winner);
                            cmdWin.Parameters.AddWithValue("@delta", winnerDelta);
                            cmdWin.ExecuteNonQuery();
                        }

                        using (var cmdLose = new SqliteCommand(@"
                            UPDATE Users
                            SET Points = MAX(0, COALESCE(Points,0) + @delta)
                            WHERE Username = @u;", conn, tran))
                        {
                            cmdLose.Parameters.AddWithValue("@u", loser);
                            cmdLose.Parameters.AddWithValue("@delta", loserDelta);
                            cmdLose.ExecuteNonQuery();
                        }

                        int winnerPoints = 0, loserPoints = 0;

                        using (var q1 = new SqliteCommand("SELECT COALESCE(Points,0) FROM Users WHERE Username = @u;", conn, tran))
                        {
                            q1.Parameters.AddWithValue("@u", winner);
                            winnerPoints = Convert.ToInt32(q1.ExecuteScalar() ?? 0);
                        }

                        using (var q2 = new SqliteCommand("SELECT COALESCE(Points,0) FROM Users WHERE Username = @u;", conn, tran))
                        {
                            q2.Parameters.AddWithValue("@u", loser);
                            loserPoints = Convert.ToInt32(q2.ExecuteScalar() ?? 0);
                        }

                        using (var histCmd = new SqliteCommand(@"
                            INSERT INTO BattleHistory
                                (Player1, Player2, Winner, EndReason, Player1AliveCount, Player2AliveCount, DateUtc)
                            VALUES (@p1, @p2, @winner, 'NORMAL', @a1, @a2, @date);", conn, tran))
                        {
                            histCmd.Parameters.AddWithValue("@p1", p1);
                            histCmd.Parameters.AddWithValue("@p2", p2);
                            histCmd.Parameters.AddWithValue("@winner", winner);
                            histCmd.Parameters.AddWithValue("@a1", alive1);
                            histCmd.Parameters.AddWithValue("@a2", alive2);
                            histCmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
                            histCmd.ExecuteNonQuery();
                        }

                        tran.Commit();

                        Console.WriteLine($"[POINTS] {winner} gana +{winnerDelta}, total => {winnerPoints}");
                        Console.WriteLine($"[POINTS] {loser} cambia {loserDelta}, total => {loserPoints}");

                        SendPrivateMessage(p1, $"BATTLE_END|{winner}|{winnerPoints}|{loser}|{loserPoints}");
                        SendPrivateMessage(p2, $"BATTLE_END|{winner}|{winnerPoints}|{loser}|{loserPoints}");
                    }

                    WriteResponse(stream, "OK|Battle end + puntos guardados.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[BATTLE_END] DB error: " + ex.Message);
                    WriteResponse(stream, "ERROR|DB error (battle_end): " + ex.Message);
                }
            }
            else if (action == "get_top5")
{
    try
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();

        using var cmd = new SqliteCommand(
            @"SELECT Username, COALESCE(Points,0) AS Points
              FROM Users
              ORDER BY Points DESC, Username ASC
              LIMIT 10;", conn);

        using var reader = cmd.ExecuteReader();

        var rows = new List<string>();
        while (reader.Read())
        {
            string user = reader.IsDBNull(0) ? "" : reader.GetString(0);
            int pts = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            if (!string.IsNullOrWhiteSpace(user))
                rows.Add($"{user}:{pts}");
        }

        WriteResponse(stream, "OK|" + string.Join(",", rows));
    }
    catch (Exception ex)
    {
        WriteResponse(stream, "ERROR|DB error: " + ex.Message);
    }
}
            else if (action == "get_most_picked_pokemon_global")
            {
                try
                {
                    using var conn = new SqliteConnection(connectionString);
                    conn.Open();

                    using var cmd = new SqliteCommand(@"
                        SELECT p.Name, COUNT(*) AS Cnt
                        FROM equipo_pokemon ep
                        JOIN Pokemon p ON p.Id = ep.pokemon_id
                        GROUP BY p.Id, p.Name
                        ORDER BY Cnt DESC
                        LIMIT 1;", conn);

                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        string name = reader.IsDBNull(0) ? "" : reader.GetString(0);
                        int cnt = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        WriteResponse(stream, $"OK|{name}:{cnt}");
                    }
                    else
                    {
                        WriteResponse(stream, "OK|:");
                    }
                }
                catch (Exception ex)
                {
                    WriteResponse(stream, "ERROR|DB error: " + ex.Message);
                }
            }
            else if (action == "get_player_stats" && parts.Length >= 2)
            {
                string username = parts[1];

                using var conn = new SqliteConnection(connectionString);
                conn.Open();

                int total = 0;
                int wins = 0;
                int losses = 0;
                int surrenders = 0;
                double avgAlive = 0;

                // Get most used pokemon and its count for this user
                string mostUsedPokemon = "";
                int mostUsedCount = 0;
                using (var cmdMost = new SqliteCommand(@"
                    SELECT p.Name, COUNT(*) AS Cnt
                    FROM equipos e
                    JOIN equipo_pokemon ep ON ep.equipo_id = e.id
                    JOIN Pokemon p ON p.Id = ep.pokemon_id
                    WHERE e.owner_username = @u
                    GROUP BY p.Id, p.Name
                    ORDER BY Cnt DESC
                    LIMIT 1;", conn))
                {
                    cmdMost.Parameters.AddWithValue("@u", username);
                    using var readerMost = cmdMost.ExecuteReader();
                    if (readerMost.Read())
                    {
                        mostUsedPokemon = readerMost.IsDBNull(0) ? "" : readerMost.GetString(0);
                        mostUsedCount = readerMost.IsDBNull(1) ? 0 : readerMost.GetInt32(1);
                    }
                }

                using (var cmd = new SqliteCommand(@"
                    SELECT 
                        COUNT(*) AS Total,
                        SUM(CASE WHEN Winner = @u THEN 1 ELSE 0 END) AS Wins,
                        -- Derrotas NORMALES (excluimos rendiciones)
                        SUM(CASE WHEN Winner IS NOT NULL 
                  AND Winner <> @u 
                  AND (Player1 = @u OR Player2 = @u)
                  AND EndReason <> 'SURRENDER'
             THEN 1 ELSE 0 END) AS Losses,
        -- Rendiciones propias (el usuario NO es el ganador y la razón es SURRENDER)
        SUM(CASE WHEN EndReason = 'SURRENDER' 
                  AND (Player1 = @u OR Player2 = @u)
                  AND Winner <> @u
             THEN 1 ELSE 0 END) AS Surrenders,
        AVG(CASE 
                WHEN Player1 = @u THEN Player1AliveCount
                WHEN Player2 = @u THEN Player2AliveCount
                ELSE NULL
            END) AS AvgAlive
    FROM BattleHistory
    WHERE Player1 = @u OR Player2 = @u;", conn))
                {
                    cmd.Parameters.AddWithValue("@u", username);
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        total      = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        wins       = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        losses     = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                        surrenders = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                        avgAlive   = reader.IsDBNull(4) ? 0.0 : reader.GetDouble(4);
                    }
                }

                double winPct = total > 0 ? (wins * 100.0 / total) : 0;
                double lossPct = total > 0 ? (losses * 100.0 / total) : 0;
                double surrenderPct = total > 0 ? (surrenders * 100.0 / total) : 0;

                var payload = new
                {
                    Username = username,
                    MostUsedPokemon = mostUsedPokemon,
                    MostUsedPokemonCount = mostUsedCount,
                    TotalBattles = total,
                    Wins = wins,
                    Losses = losses,
                    Surrenders = surrenders,
                    WinPct = winPct,
                    LossPct = lossPct,
                    SurrenderPct = surrenderPct,
                    AvgAlive = avgAlive
                };

                string json = JsonSerializer.Serialize(payload);
                WriteResponse(stream, "OK|" + json);
            }
            else
            {
                WriteResponse(stream, "ERROR|Acción no reconocida.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("ProcessMessage error: " + ex.Message);
            try { WriteResponse(stream, "ERROR|Server exception: " + ex.Message); } catch { }
        }
    }

    // NUEVO: crear equipo + relacionar pokmon
    static string CreateTeam(string ownerUsername, string name, int[] pokemonIds)
    {
        if (string.IsNullOrWhiteSpace(ownerUsername))
            return "ERROR|Usuario inválido.";

        if (string.IsNullOrWhiteSpace(name))
            return "ERROR|Nombre de equipo inválido.";

        if (pokemonIds == null || pokemonIds.Length == 0)
            return "ERROR|El equipo debe tener al menos un Pokmon.";

        if (pokemonIds.Length > 6)
            return "ERROR|El equipo no puede tener ms de 6 Pokmon.";

        try
        {
            using (var conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                using (var tran = conn.BeginTransaction())
                {
                    long equipoId;
                    using (var cmd = new SqliteCommand(
                        "INSERT INTO equipos (nombre, owner_username) VALUES (@n, @u); SELECT last_insert_rowid();",
                        conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@n", name);
                        cmd.Parameters.AddWithValue("@u", ownerUsername);
                        equipoId = (long)cmd.ExecuteScalar();
                    }

                    using (var cmdEp = new SqliteCommand(
                        "INSERT INTO equipo_pokemon (equipo_id, pokemon_id) VALUES (@e,@p);",
                        conn, tran))
                    {
                        var pEquipo = cmdEp.Parameters.Add("@e", SqliteType.Integer);
                        var pPokemon = cmdEp.Parameters.Add("@p", SqliteType.Integer);

                        pEquipo.Value = equipoId;

                        foreach (var id in pokemonIds)
                        {
                            pPokemon.Value = id;
                            cmdEp.ExecuteNonQuery();
                        }
                    }

                    tran.Commit();
                }
            }

            return "OK|Equipo creado correctamente.";
        }
        catch (Exception ex)
        {
            return "ERROR|No se pudo crear el equipo: " + ex.Message;
        }
    }

    static void WriteResponse(NetworkStream stream, string response)
    {
        try
        {
            if (stream != null && stream.CanWrite)
            {
                var data = Encoding.UTF8.GetBytes(response + "\n");
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
        }
        catch { /* ignore write errors */ }
    }

    static void BroadcastMessage(string sender, string text)
    {
        string payload = $"MSG|{sender}|{text}\n";
        byte[] data = Encoding.UTF8.GetBytes(payload);

        List<string> toRemove = new();
        lock (subsLock)
        {
            foreach (var kv in subscribers.ToList())
            {
                var info = kv.Value;
                try
                {
                    if (info.Stream != null && info.Stream.CanWrite)
                    {
                        info.Stream.Write(data, 0, data.Length);
                        info.Stream.Flush();
                    }
                }
                catch
                {
                    toRemove.Add(kv.Key);
                }
            }

            // Cleanup dead subscribers
            foreach (var u in toRemove)
            {
                if (subscribers.Remove(u, out var removed))
                {
                    try { removed.Stream.Close(); removed.Client.Close(); } catch { }
                    Console.WriteLine($"{u} removed due to write failure.");
                }
            }
        }

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {sender}: {text}");
    }

    static void SendPrivateMessage(string targetUser, string payload)
    {
        byte[] data = Encoding.UTF8.GetBytes(payload + "\n");
        lock (subsLock)
        {
            if (subscribers.TryGetValue(targetUser, out var info))
            {
                try
                {
                    if (info.Stream != null && info.Stream.CanWrite)
                    {
                        info.Stream.Write(data, 0, data.Length);
                        info.Stream.Flush();
                    }
                }
                catch
                {
                    // Si falla, limpiar suscriptor
                    if (subscribers.Remove(targetUser, out var removed))
                    {
                        try { removed.Stream.Close(); removed.Client.Close(); } catch { }
                        Console.WriteLine($"{targetUser} removed due to write failure (private).");
                    }
                }
            }
        }
    }

    static string GetUsernameByStream(NetworkStream stream)
    {
        lock (subsLock)
        {
            foreach (var kv in subscribers)
            {
                if (object.ReferenceEquals(kv.Value.Stream, stream))
                    return kv.Key;
            }
        }
        return null;
    }

    // New helper: match sender by TcpClient reference (more reliable than networkstream equality in some cases)
    static string GetUsernameByClient(TcpClient client)
    {
        if (client == null) return null;
        lock (subsLock)
        {
            foreach (var kv in subscribers)
            {
                if (object.ReferenceEquals(kv.Value.Client, client))
                    return kv.Key;
            }
        }
        return null;
    }

    static void CleanupSubscriberByStream(NetworkStream stream)
    {
        if (stream == null) return;
        string toRemove = null;
        lock (subsLock)
        {
            foreach (var kv in subscribers)
            {
                if (object.ReferenceEquals(kv.Value.Stream, stream))
                {
                    toRemove = kv.Key;
                    break;
                }
            }
            if (toRemove != null && subscribers.Remove(toRemove, out var info))
            {
                try { info.Stream.Close(); info.Client.Close(); } catch { }
                Console.WriteLine($"{toRemove} desconectado (stream closed).");
                lock (usersLock)
                {
                    if (connectedUsers.Remove(toRemove))
                    {
                        Console.WriteLine($"{toRemove} removed from connectedUsers.");
                    }
                }
            }
        }
    }

    // New helper: cleanup by TcpClient reference (don't call GetStream() on a posiblemente disposed client)
    static void CleanupSubscriberByClient(TcpClient client)
    {
        if (client == null) return;
        string toRemove = null;
        lock (subsLock)
        {
            foreach (var kv in subscribers)
            {
                if (object.ReferenceEquals(kv.Value.Client, client))
                {
                    toRemove = kv.Key;
                    break;
                }
            }
            if (toRemove != null && subscribers.Remove(toRemove, out var info))
            {
                try { info.Stream?.Close(); info.Client?.Close(); } catch { }
                Console.WriteLine($"{toRemove} desconectado (client closed).");
                lock (usersLock)
                {
                    if (connectedUsers.Remove(toRemove))
                    {
                        Console.WriteLine($"{toRemove} removed from connectedUsers.");
                    }
                }
            }
        }
    }

    // ====================== COMBATES (memoria) ======================
    // battleKey estable: "A|B" ordenado alfabéticamente (case-insensitive)
    static readonly Dictionary<string, BattleState> battles = new(StringComparer.OrdinalIgnoreCase);
    static readonly object battlesLock = new();

    record BattleState(string PlayerA, string PlayerB, string? TeamA, string? TeamB);
    record BattleStateWithResult(string PlayerA, string PlayerB, string? TeamA, string? TeamB, bool Ended);

    static string MakeBattleKey(string p1, string p2)
    {
        return string.Compare(p1, p2, StringComparison.OrdinalIgnoreCase) <= 0
            ? $"{p1}|{p2}"
            : $"{p2}|{p1}";
    }
}
