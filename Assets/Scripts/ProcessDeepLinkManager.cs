using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace Assets.Scripts
{
    public class ProcessDeepLinkManager : MonoBehaviour
    {
        [SerializeField]
        Text m_LogText;

        public Text logText
        {
            get { return m_LogText; }
            set { m_LogText = value; }
        }

        public void Log(string message)
        {
            Debug.LogError(message);

            m_LogText.text += $"{message}\n";
        }

        public static ProcessDeepLinkManager Instance { get; private set; }

        /// <summary>
        /// Meshes imported from loaded OBJ file.
        /// </summary>
        public Mesh[] LoadedMeshes { get; private set; }

        public string deeplinkURL;

        private void Awake()
        {
            if (Instance == null)
            {
                Log("Register deep link handler");

                Instance = this;
                Application.deepLinkActivated += HandleLink;
                if (!string.IsNullOrEmpty(Application.absoluteURL))
                {
                    // Cold start and Application.absoluteURL not null so process Deep Link.
                    HandleLink(Application.absoluteURL);
                }
                // Initialize DeepLink Manager global variable.
                else deeplinkURL = "[none]";
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private IEnumerator LoadDataFromServer(string url, Action<byte[]> response)
        {
            var stopwatch = Stopwatch.StartNew();

            Log($"Loading text from {url}");
            var request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();

            Log($"Loaded in {stopwatch.Elapsed.TotalSeconds:F3} sec");
            if (! request.isHttpError && ! request.isNetworkError)
            {
                response(request.downloadHandler.data);
            }
            else
            {
                Log($"Error request [{url}, {request.error}]");
                response(null);
            }

            request.Dispose();
        }

        private void HandleLink(string url)
        {
            Log("Link activated: " + url);

            // Update DeepLink Manager global variable, so URL can be accessed from anywhere.
            deeplinkURL = url.Replace("ld2020", "http");

            StartCoroutine(LoadDataFromServer(deeplinkURL, content =>
            {
                if (content != null)
                {
                    try
                    {
                        var stopwatch = Stopwatch.StartNew();

                        // read OBJ file from ZIP
                        using (var ms = new MemoryStream(content))
                        using (var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false))
                        {
                            ZipArchiveEntry entry = zip.GetEntry("result.obj");
                            if (entry == null)
                            {
                                Log("Cannot find OBJ file");
                                return;
                            }

                            using (var reader = new StreamReader(entry.Open()))
                            {
                                LoadedMeshes = ObjImporter.Process(reader.ReadToEnd());
                            }
                        }

                        Log($"Imported {LoadedMeshes.Length} mesh(es) in {stopwatch.Elapsed.TotalSeconds:F3} sec");
                    }
                    catch (Exception e)
                    {
                        Log(e.ToString());
                    }
                }
            }));
        }
    }
}
