using UnityEngine;

namespace Assets.Scripts
{
    public class ProcessDeepLinkManager : MonoBehaviour
    {
        public static ProcessDeepLinkManager Instance { get; private set; }

        public string deeplinkURL;
        private void Awake()
        {
            if (Instance == null)
            {
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
 
        private void onDeepLinkActivated(string url)
        {
            // Update DeepLink Manager global variable, so URL can be accessed from anywhere.
            deeplinkURL = url;

            Debug.Log("**************** Link activated: " + url);
        
//// Decode the URL to determine action. 
//// In this example, the app expects a link formatted like this:
//// unitydl://mylink?scene1
//            string sceneName = url.Split("?"[0])[1];
//            bool validScene;
//            switch (sceneName)
//            {
//                case "scene1":
//                    validScene = true;
//                    break;
//                case "scene2":
//                    validScene = true;
//                    break;
//                default:
//                    validScene = false;
//                    break;
//            }
//            if (validScene) SceneManager.LoadScene(sceneName);
        }
    }
}
