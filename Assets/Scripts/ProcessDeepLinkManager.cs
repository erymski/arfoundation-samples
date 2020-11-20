using System;
using System.Collections;
using System.Diagnostics;
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

        public Mesh LoadedMesh { get; private set; }

        public string deeplinkURL;
        private void Awake()
        {
            if (Instance == null)
            {
                Log("Register deep link handler");

                Instance = this;
                Application.deepLinkActivated += onDeepLinkActivated;
                if (!string.IsNullOrEmpty(Application.absoluteURL))
                {
                    // Cold start and Application.absoluteURL not null so process Deep Link.
                    onDeepLinkActivated(Application.absoluteURL);
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

        IEnumerator LoadTextFromServer(string url, Action<string> response)
        {
            var stopwatch = Stopwatch.StartNew();
            Log($"Loading text from {url}");
            var request = UnityWebRequest.Get(url);

            yield return request.SendWebRequest();
            Log($"Loaded in {stopwatch.Elapsed.TotalSeconds:F3} sec");

            if (! request.isHttpError && ! request.isNetworkError)
            {
                response(request.downloadHandler.text);
            }
            else
            {
                Log($"Error request [{url}, {request.error}]");
       
                response(null);
            }

            request.Dispose();
        }

        private void onDeepLinkActivated(string url)
        {
            Log("Link activated: " + url);

            // Update DeepLink Manager global variable, so URL can be accessed from anywhere.
            deeplinkURL = url.Replace("ld2020", "http");

            StartCoroutine(LoadTextFromServer(deeplinkURL, content =>
            {
                if (content != null)
                {
                    var stopwatch = Stopwatch.StartNew();
                    LoadedMesh = FastObjImporter.Instance.ImportContent(content);
                    Log($"Imported mesh with {LoadedMesh.vertices.Length} vertices in {stopwatch.Elapsed.TotalSeconds:F3} sec");
                }
            }));
        }
    }
}
