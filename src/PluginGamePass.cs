using BepInEx;
using BepInEx.Unity.IL2CPP;
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
using UnityEngine;

namespace InteractifLive.PlanetCrafter.GamePass;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class PluginGamePass : BasePlugin
{
    public const string PluginGuid = "jesink.interactiflive.planet-crafter.gamepass";
    public const string PluginName = "Interactif Live - The Planet Crafter Game Pass";
    public const string PluginVersion = "0.2.2";
    private const string Prefix = "http://127.0.0.1:18948/";
    private readonly ConcurrentQueue<string> queue = new();
    private HttpListener listener;
    private CancellationTokenSource stop;
    private static PluginGamePass Instance;
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "restore_oxygen", "restore_water", "restore_food", "restore_health",
        "drain_oxygen", "drain_water", "drain_food", "damage_player",
        "give_random_item", "give_random_items_5", "give_random_items_10",
        "meteor_shower_beneficial", "boost_terraform", "repair_nearby_machines",
        "meteor_storm", "bad_weather", "remove_random_item", "disable_nearby_machines",
        "surprise_teleport", "slow_player"
    };
    private static readonly HashSet<string> Implemented = new(StringComparer.OrdinalIgnoreCase)
    {
        "restore_oxygen", "restore_water", "restore_food", "restore_health",
        "drain_oxygen", "drain_water", "drain_food", "damage_player"
    };

    public override void Load()
    {
        Instance = this;
        Log.LogInfo($"{PluginName} {PluginVersion} chargé (BepInEx 6 IL2CPP).");
        stop = new CancellationTokenSource();
        listener = new HttpListener();
        listener.Prefixes.Add(Prefix);
        listener.Start();
        _ = Task.Run(() => ListenLoop(stop.Token));
        // HTTP callbacks run on a worker thread. Unity/IL2CPP game objects
        // must only be inspected or modified from Unity's main thread.
        AddComponent<MainThreadRunner>();
        Log.LogInfo($"Pont local actif sur {Prefix}");
    }

    private async Task ListenLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && listener.IsListening)
        {
            try { var context = await listener.GetContextAsync(); _ = Task.Run(() => Handle(context), token); }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) { break; }
            catch (Exception ex) { Log.LogWarning(ex.Message); }
        }
    }

    private void Handle(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url.AbsolutePath;
            string body;
            if (context.Request.HttpMethod == "GET" && path == "/health")
                body = $"{{\"success\":true,\"plugin\":\"{PluginGuid}\",\"version\":\"{PluginVersion}\",\"runtime\":\"il2cpp\",\"ready\":true}}";
            else if (context.Request.HttpMethod == "POST" && path == "/action")
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var action = ReadJsonValue(reader.ReadToEnd(), "action");
                if (!Allowed.Contains(action)) { context.Response.StatusCode = 400; body = "{\"success\":false,\"error\":\"Action inconnue\"}"; }
                else if (!Implemented.Contains(action)) { context.Response.StatusCode = 501; body = "{\"success\":false,\"error\":\"Action non encore compatible avec cette version IL2CPP\"}"; }
                else { queue.Enqueue(action); body = $"{{\"success\":true,\"accepted\":true,\"action\":\"{Escape(action)}\"}}"; }
            }
            else { context.Response.StatusCode = 404; body = "{\"success\":false,\"error\":\"Route inconnue\"}"; }
            var bytes = Encoding.UTF8.GetBytes(body); context.Response.ContentType = "application/json; charset=utf-8"; context.Response.ContentLength64 = bytes.Length; context.Response.OutputStream.Write(bytes, 0, bytes.Length); context.Response.Close();
        }
        catch (Exception ex) { Log.LogWarning($"Requête refusée : {ex.Message}"); try { context.Response.StatusCode = 500; context.Response.Close(); } catch { } }
    }

    private void ProcessQueuedActions()
    {
        while (queue.TryDequeue(out var action))
        {
            try { Log.LogInfo($"Action {action} reçue : {ExecuteAction(action)}"); }
            catch (Exception ex) { Log.LogWarning($"Action {action} non exécutée : {ex.Message}"); }
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
            default: return "reçue ; mapping IL2CPP de cette action à finaliser";
        }
    }

    private static void AddGauge(string methodName, int amount)
    {
        var managers = FindType("Managers"); var playersManager = FindType("PlayersManager");
        if (managers == null || playersManager == null) throw new InvalidOperationException("Managers ou PlayersManager introuvable");
        var getManager = managers.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "GetManager" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1);
        if (getManager == null) throw new MissingMethodException("Managers.GetManager<T>");
        var playerManager = getManager.MakeGenericMethod(playersManager).Invoke(null, null); var player = Invoke(playerManager, "GetActivePlayerController"); var gauges = Invoke(player, "GetGaugesHandler");
        var method = gauges.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance); if (method == null) throw new MissingMethodException(methodName); method.Invoke(gauges, new object[] { amount });
    }
    private static object Invoke(object target, string name) { if (target == null) throw new InvalidOperationException(name + " : cible introuvable"); var method = target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); if (method == null) throw new MissingMethodException(name); return method.Invoke(target, null); }
    private static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeTypes).FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    private static IEnumerable<Type> SafeTypes(Assembly assembly) { try { return assembly.GetTypes(); } catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); } catch { return Array.Empty<Type>(); } }
    private static string ReadJsonValue(string body, string key) { var marker = "\"" + key + "\""; var start = body.IndexOf(marker, StringComparison.OrdinalIgnoreCase); if (start < 0) return ""; start = body.IndexOf(':', start); start = body.IndexOf('"', start); var end = body.IndexOf('"', start + 1); return end > start ? body.Substring(start + 1, end - start - 1) : ""; }
    private static string Escape(string value) => (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

    private sealed class MainThreadRunner : MonoBehaviour
    {
        private void Update()
        {
            Instance?.ProcessQueuedActions();
        }
    }
}
