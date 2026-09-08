using BepInEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InteractifLive.PlanetCrafter;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "jesink.interactiflive.planet-crafter";
    public const string PluginName = "Interactif Live - The Planet Crafter";
    public const string PluginVersion = "0.2.0";
    private const string Prefix = "http://127.0.0.1:18948/";
    private readonly ConcurrentQueue<PendingAction> queue = new();
    private HttpListener listener;
    private CancellationTokenSource stop;

    private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "restore_oxygen", "restore_water", "restore_food", "restore_health",
        "give_random_item", "give_random_items_5", "give_random_items_10",
        "meteor_shower_beneficial", "boost_terraform", "repair_nearby_machines",
        "drain_oxygen", "drain_water", "drain_food", "damage_player",
        "meteor_storm", "bad_weather", "remove_random_item", "disable_nearby_machines",
        "surprise_teleport", "slow_player"
    };
    private static readonly HashSet<string> ImplementedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "restore_oxygen", "restore_water", "restore_food", "restore_health",
        "drain_oxygen", "drain_water", "drain_food", "damage_player"
    };

    public void Awake()
    {
        Logger.LogInfo($"{PluginName} {PluginVersion} chargé.");
        StartBridge();
    }

    private void StartBridge()
    {
        try
        {
            stop = new CancellationTokenSource();
            listener = new HttpListener();
            listener.Prefixes.Add(Prefix);
            listener.Start();
            _ = Task.Run(() => ListenLoop(stop.Token));
            Logger.LogInfo($"Pont local actif sur {Prefix}");
        }
        catch (Exception ex) { Logger.LogError($"Pont Planet Crafter impossible : {ex.Message}"); }
    }

    private async Task ListenLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && listener != null && listener.IsListening)
        {
            try
            {
                var context = await listener.GetContextAsync();
                if (!token.IsCancellationRequested) _ = Task.Run(() => Handle(context), token);
            }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) { break; }
            catch (Exception ex) { Logger.LogWarning(ex.Message); }
        }
    }

    private void Handle(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url.AbsolutePath;
            string body;
            if (context.Request.HttpMethod == "GET" && path == "/health")
                body = $"{{\"success\":true,\"plugin\":\"{PluginGuid}\",\"version\":\"{PluginVersion}\",\"ready\":true}}";
            else if (context.Request.HttpMethod == "POST" && path == "/action")
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var payload = Parse(reader.ReadToEnd());
                if (!AllowedActions.Contains(payload.Action ?? ""))
                {
                    context.Response.StatusCode = 400;
                    body = "{\"success\":false,\"error\":\"Action Planet Crafter inconnue ou non autorisée\"}";
                }
                else if (!ImplementedActions.Contains(payload.Action ?? ""))
                {
                    context.Response.StatusCode = 501;
                    body = "{\"success\":false,\"error\":\"Action non encore compatible avec cette version Mono\"}";
                }
                else
                {
                    queue.Enqueue(new PendingAction(payload.Action, payload.Donor));
                    body = $"{{\"success\":true,\"accepted\":true,\"action\":\"{Escape(payload.Action)}\",\"message\":\"Action placée dans la file Unity\"}}";
                }
            }
            else
            {
                context.Response.StatusCode = 404;
                body = "{\"success\":false,\"error\":\"Route inconnue\"}";
            }
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Requête refusée : {ex.Message}");
            try { context.Response.StatusCode = 500; context.Response.Close(); } catch { }
        }
    }

    private void Update()
    {
        while (queue.TryDequeue(out var item))
        {
            try { Logger.LogInfo($"Action {item.Action} reçue de {item.Donor} : {ExecuteAction(item.Action)}"); }
            catch (Exception ex) { Logger.LogWarning($"Action {item.Action} non exécutée : {ex.Message}"); }
        }
    }

    private string ExecuteAction(string action)
    {
        switch (action)
        {
            case "restore_oxygen": AddGauge("AddOxygen", 1000); return "oxygène restauré";
            case "restore_water": AddGauge("AddWater", 100); return "eau restaurée";
            case "restore_food": AddGauge("AddFood", 100); return "nourriture restaurée";
            case "restore_health": AddGauge("AddHealth", 100); return "santé restaurée";
            case "drain_oxygen": AddGauge("AddOxygen", -1000); return "oxygène réduit";
            case "drain_water": AddGauge("AddWater", -100); return "eau réduite";
            case "drain_food": AddGauge("AddFood", -100); return "nourriture réduite";
            case "damage_player": AddGauge("AddHealth", -25); return "dégâts appliqués";
            default: return "reçue ; mapping de l’API interne à finaliser sur la version installée";
        }
    }

    private void AddGauge(string methodName, int amount)
    {
        var managers = FindType("Managers");
        var playersManager = FindType("PlayersManager");
        if (managers == null || playersManager == null) throw new InvalidOperationException("Managers ou PlayersManager introuvable");
        var getManager = managers.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "GetManager" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1);
        if (getManager == null) throw new MissingMethodException("Managers.GetManager<T>");
        var playerManager = getManager.MakeGenericMethod(playersManager).Invoke(null, null);
        var player = Invoke(playerManager, "GetActivePlayerController");
        var gauges = Invoke(player, "GetGaugesHandler");
        if (gauges == null) throw new InvalidOperationException("GaugesHandler introuvable");
        var method = gauges.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        if (method == null) throw new MissingMethodException(methodName);
        method.Invoke(gauges, new object[] { amount });
    }

    private static object Invoke(object target, string method)
    {
        if (target == null) throw new InvalidOperationException(method + " : cible introuvable");
        var info = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (info == null) throw new MissingMethodException(method);
        return info.Invoke(target, null);
    }

    private static Type FindType(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeTypes)
            .FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
        catch { return Array.Empty<Type>(); }
    }

    private void OnDestroy()
    {
        try { stop?.Cancel(); listener?.Stop(); listener?.Close(); } catch { }
    }

    private sealed class PendingAction
    {
        public readonly string Action;
        public readonly string Donor;
        public PendingAction(string action, string donor) { Action = action ?? ""; Donor = donor ?? "spectateur"; }
    }

    private sealed class BridgeAction { public string Action { get; set; } public string Donor { get; set; } }
    private static string Escape(string value) => (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static BridgeAction Parse(string body)
    {
        string Read(string key)
        {
            var marker = "\"" + key + "\"";
            var start = body.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return "";
            start = body.IndexOf(':', start); if (start < 0) return "";
            start = body.IndexOf('"', start); if (start < 0) return "";
            var end = body.IndexOf('"', start + 1);
            return end > start ? body.Substring(start + 1, end - start - 1) : "";
        }
        return new BridgeAction { Action = Read("action"), Donor = Read("donor") };
    }
}
