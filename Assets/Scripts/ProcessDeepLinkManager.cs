﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Assets.Scripts
{
    public sealed class FastObjImporter
    {
        private sealed class Vector3Int
        {
            public int x { get; set; }
            public int y { get; set; }
            public int z { get; set; }

            public Vector3Int() { }

            public Vector3Int(int x, int y, int z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }
        }

        #region singleton
        // Singleton code
        // Static can be called from anywhere without having to make an instance
        private static FastObjImporter _instance;

        // If called check if there is an instance, otherwise create it
        public static FastObjImporter Instance
        {
            get { return _instance ?? (_instance = new FastObjImporter()); }
        }
        #endregion

        private List<int> triangles;
        private List<Vector3> vertices;
        private List<Vector2> uv;
        private List<Vector3> normals;
        private List<Vector3Int> faceData;
        private List<int> intArray;

        private const int MIN_POW_10 = -16;
        private const int MAX_POW_10 = 16;
        private const int NUM_POWS_10 = MAX_POW_10 - MIN_POW_10 + 1;
        private static readonly float[] pow10 = GenerateLookupTable();

        // Use this for initialization
        public Mesh ImportContent(string content)
        {
            triangles = new List<int>();
            vertices = new List<Vector3>();
            uv = new List<Vector2>();
            normals = new List<Vector3>();
            faceData = new List<Vector3Int>();
            intArray = new List<int>();

            LoadMeshData(content);

            Vector3[] newVerts = new Vector3[faceData.Count];
            Vector2[] newUVs = new Vector2[faceData.Count];
            Vector3[] newNormals = new Vector3[faceData.Count];

            /* The following foreach loops through the facedata and assigns the appropriate vertex, uv, or normal
                 * for the appropriate Unity mesh array.
                 */
            for (int i = 0; i < faceData.Count; i++)
            {
                newVerts[i] = vertices[faceData[i].x - 1];
                if (faceData[i].y >= 1)
                    newUVs[i] = uv[faceData[i].y - 1];

                if (faceData[i].z >= 1)
                    newNormals[i] = normals[faceData[i].z - 1];
            }

            Mesh mesh = new Mesh();

            mesh.vertices = newVerts;
            mesh.uv = newUVs;
            mesh.normals = newNormals;
            mesh.triangles = triangles.ToArray();

            mesh.RecalculateBounds();
            mesh.Optimize();

            return mesh;
        }

        private void LoadMeshData(string text)
        {
            StringBuilder sb = new StringBuilder();
            int start = 0;
            string objectName = null;
            int faceDataCount = 0;

            StringBuilder sbFloat = new StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    sb.Remove(0, sb.Length);

                    // Start +1 for whitespace '\n'
                    sb.Append(text, start + 1, i - start);
                    start = i;

                    if (sb[0] == 'o' && sb[1] == ' ')
                    {
                        sbFloat.Remove(0, sbFloat.Length);
                        int j = 2;
                        while (j < sb.Length)
                        {
                            objectName += sb[j];
                            j++;
                        }
                    }
                    else if (sb[0] == 'v' && sb[1] == ' ') // Vertices
                    {
                        int splitStart = 2;

                        vertices.Add(new Vector3(GetFloat(sb, ref splitStart, ref sbFloat),
                            GetFloat(sb, ref splitStart, ref sbFloat), GetFloat(sb, ref splitStart, ref sbFloat)));
                    }
                    else if (sb[0] == 'v' && sb[1] == 't' && sb[2] == ' ') // UV
                    {
                        int splitStart = 3;

                        uv.Add(new Vector2(GetFloat(sb, ref splitStart, ref sbFloat),
                            GetFloat(sb, ref splitStart, ref sbFloat)));
                    }
                    else if (sb[0] == 'v' && sb[1] == 'n' && sb[2] == ' ') // Normals
                    {
                        int splitStart = 3;

                        normals.Add(new Vector3(GetFloat(sb, ref splitStart, ref sbFloat),
                            GetFloat(sb, ref splitStart, ref sbFloat), GetFloat(sb, ref splitStart, ref sbFloat)));
                    }
                    else if (sb[0] == 'f' && sb[1] == ' ')
                    {
                        int splitStart = 2;

                        int j = 1;
                        intArray.Clear();
                        int info = 0;
                        // Add faceData, a face can contain multiple triangles, facedata is stored in following order vert, uv, normal. If uv or normal are / set it to a 0
                        while (splitStart < sb.Length && char.IsDigit(sb[splitStart]))
                        {
                            faceData.Add(new Vector3Int(GetInt(sb, ref splitStart, ref sbFloat),
                                GetInt(sb, ref splitStart, ref sbFloat), GetInt(sb, ref splitStart, ref sbFloat)));
                            j++;

                            intArray.Add(faceDataCount);
                            faceDataCount++;
                        }

                        info += j;
                        j = 1;
                        while (j + 2 < info) //Create triangles out of the face data.  There will generally be more than 1 triangle per face.
                        {
                            triangles.Add(intArray[0]);
                            triangles.Add(intArray[j]);
                            triangles.Add(intArray[j + 1]);

                            j++;
                        }
                    }
                }
            }
        }

        private float GetFloat(StringBuilder sb, ref int start, ref StringBuilder sbFloat)
        {
            sbFloat.Remove(0, sbFloat.Length);
            while (start < sb.Length &&
                   (char.IsDigit(sb[start]) || sb[start] == '-' || sb[start] == '.'))
            {
                sbFloat.Append(sb[start]);
                start++;
            }
            start++;

            return ParseFloat(sbFloat);
        }

        private int GetInt(StringBuilder sb, ref int start, ref StringBuilder sbInt)
        {
            sbInt.Remove(0, sbInt.Length);
            while (start < sb.Length &&
                   (char.IsDigit(sb[start])))
            {
                sbInt.Append(sb[start]);
                start++;
            }
            start++;

            return IntParseFast(sbInt);
        }


        private static float[] GenerateLookupTable()
        {
            var result = new float[(-MIN_POW_10 + MAX_POW_10) * 10];
            for (int i = 0; i < result.Length; i++)
                result[i] = (float)((i / NUM_POWS_10) *
                                    Mathf.Pow(10, i % NUM_POWS_10 + MIN_POW_10));
            return result;
        }

        private float ParseFloat(StringBuilder value)
        {
            float result = 0;
            bool negate = false;
            int len = value.Length;
            int decimalIndex = value.Length;
            for (int i = len - 1; i >= 0; i--)
                if (value[i] == '.')
                { decimalIndex = i; break; }
            int offset = -MIN_POW_10 + decimalIndex;
            for (int i = 0; i < decimalIndex; i++)
                if (i != decimalIndex && value[i] != '-')
                    result += pow10[(value[i] - '0') * NUM_POWS_10 + offset - i - 1];
                else if (value[i] == '-')
                    negate = true;
            for (int i = decimalIndex + 1; i < len; i++)
                if (i != decimalIndex)
                    result += pow10[(value[i] - '0') * NUM_POWS_10 + offset - i];
            if (negate)
                result = -result;
            return result;
        }

        private int IntParseFast(StringBuilder value)
        {
            // An optimized int parse method.
            int result = 0;
            for (int i = 0; i < value.Length; i++)
            {
                result = 10 * result + (value[i] - 48);
            }
            return result;
        }
    }

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
            Log($"Loading text from {url}");
            var request = UnityWebRequest.Get(url);

            yield return request.SendWebRequest();

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
            // Update DeepLink Manager global variable, so URL can be accessed from anywhere.
            deeplinkURL = url;

            Log("Link activated: " + url);

            string urlObj = @"http://defa38f68794.ngrok.io/container.obj";
            StartCoroutine(LoadTextFromServer(urlObj, content =>
            {
                if (content != null)
                {
                    Log("Content loaded");
                    LoadedMesh = FastObjImporter.Instance.ImportContent(content);
                    Log($"Imported mesh with {LoadedMesh.vertices.Length} vertices");
                }
            }));

            


//            Mesh myMesh = FastObjImporter.Instance.ImportContent("path_to_obj_file.obj");

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
