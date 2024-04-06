using System.Net;
using System.Threading;
using BepInEx;
using HarmonyLib;
using System;
using System.IO;
using Sirenix.Serialization;

namespace MyFirstPlugin;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public class Stats {
        public float score;
        public string views;
    }

    // write function to send data as json to server
    public static void SendData(string json)
    {
        // create a new instance of WebClient
        using (WebClient client = new WebClient())
        {
            // set the content type to application/json
            client.Headers[HttpRequestHeader.ContentType] = "application/json";

            // create a new thread to send the data asynchronously
            Thread thread = new Thread(() =>
            {
                client.UploadString("http://localhost:8000/v1/upload", json);
            });

            // start the thread
            thread.Start();
        }
    }
    private readonly Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
    private void Awake()
    {
        harmony.PatchAll();
        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

[HarmonyPatch(typeof(UploadVideoStation))]
internal class UploadVideoStationPatch
{
    [HarmonyPatch("RPC_UploadFlashcard")]
    [HarmonyPrefix]
    public static bool RPC_UploadFlashcardPrefix(byte[] videoHandleData, ref float __state)
    {
        // Capture the score before the original method is executed
        // and store it in __state
        VideoHandle tVideoID = new VideoHandle(new Guid(videoHandleData));
        if (RecordingsHandler.TryGetRecording(tVideoID, out var recording))
        {
            if (ContentEvaluator.EvaluateRecording(recording, out var buffer))
            {
                __state = buffer.GetScore(); // Capture the score
                return true; // Continue executing the original method
            }
        }
        return false; // Skip executing the original method
    }

    [HarmonyPatch("RPC_UploadFlashcard")]
    [HarmonyPostfix]
    public static void RPC_UploadFlashcardPostfix(float __state, byte[] videoHandleData)

    {
        // Add any additional properties or data to the stats object as needed
        var stats = new Stats
        {
            score = __state,
            views = BigNumbers.ViewsToString(BigNumbers.GetScoreToViews(__state, GameAPI.CurrentDay))
        };

        // Serialize stats object to JSON
        var json = UnityEngine.JsonUtility.ToJson(stats);

        Plugin.SendData(json);
    }
}

}
