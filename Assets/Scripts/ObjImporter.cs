using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts
{
    public sealed class ObjImporter
    {
        private sealed class Vector3Int
        {
            public int x { get; }
            public int y { get; }
            public int z { get; }

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
        private static ObjImporter _instance;

        // If called check if there is an instance, otherwise create it
        public static ObjImporter Instance
        {
            get { return _instance ?? (_instance = new ObjImporter()); }
        }
        #endregion

        private const int MIN_POW_10 = -16;
        private const int MAX_POW_10 = 16;
        private const int NUM_POWS_10 = MAX_POW_10 - MIN_POW_10 + 1;
        private static readonly float[] pow10 = GenerateLookupTable();

        // Use this for initialization
        public Mesh ImportContent(string content)
        {
            return LoadMeshData(content);
        }

        private class MeshCollector
        {
            public readonly List<int> triangles = new List<int>();
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector3Int> faceData = new List<Vector3Int>();
            public readonly List<int> intArray = new List<int>();

            public Mesh ToMesh()
            {
                var newVerts = new Vector3[faceData.Count];
                var newUVs = new Vector2[faceData.Count];
                var newNormals = new Vector3[faceData.Count];

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

                var mesh = new Mesh
                {
                    vertices = newVerts,
                    uv = newUVs,
                    normals = newNormals,
                    triangles = triangles.ToArray()
                };


                mesh.RecalculateBounds();
                mesh.Optimize();
                return mesh;
            }
        }

        private static Mesh LoadMeshData(string text)
        {
            var collector = new MeshCollector();

            var sb = new StringBuilder();

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

                    var cmd = sb[0];
                    if (cmd == 'o' && sb[1] == ' ')
                    {
                        sbFloat.Remove(0, sbFloat.Length);
                        int j = 2;
                        while (j < sb.Length)
                        {
                            objectName += sb[j];
                            j++;
                        }
                    }
                    else if (cmd == 'v' && sb[1] == ' ') // Vertices
                    {
                        int splitStart = 2;

                        collector.vertices.Add(new Vector3(GetFloat(sb, ref splitStart, ref sbFloat),
                            GetFloat(sb, ref splitStart, ref sbFloat), GetFloat(sb, ref splitStart, ref sbFloat)));
                    }
                    else if (cmd == 'v' && sb[1] == 't' && sb[2] == ' ') // UV
                    {
                        int splitStart = 3;

                        collector.uv.Add(new Vector2(GetFloat(sb, ref splitStart, ref sbFloat),
                            GetFloat(sb, ref splitStart, ref sbFloat)));
                    }
                    else if (cmd == 'v' && sb[1] == 'n' && sb[2] == ' ') // Normals
                    {
                        int splitStart = 3;

                        collector.normals.Add(new Vector3(GetFloat(sb, ref splitStart, ref sbFloat),
                            GetFloat(sb, ref splitStart, ref sbFloat), GetFloat(sb, ref splitStart, ref sbFloat)));
                    }
                    else if (cmd == 'f' && sb[1] == ' ')
                    {
                        int splitStart = 2;

                        int j = 1;
                        collector.intArray.Clear();
                        int info = 0;
                        // Add faceData, a face can contain multiple triangles, facedata is stored in following order vert, uv, normal. If uv or normal are / set it to a 0
                        while (splitStart < sb.Length && char.IsDigit(sb[splitStart]))
                        {
                            collector.faceData.Add(new Vector3Int(GetInt(sb, ref splitStart, ref sbFloat),
                                GetInt(sb, ref splitStart, ref sbFloat), GetInt(sb, ref splitStart, ref sbFloat)));
                            j++;

                            collector.intArray.Add(faceDataCount);
                            faceDataCount++;
                        }

                        info += j;
                        j = 1;
                        while (j + 2 < info) //Create triangles out of the face data.  There will generally be more than 1 triangle per face.
                        {
                            collector.triangles.Add(collector.intArray[0]);
                            collector.triangles.Add(collector.intArray[j]);
                            collector.triangles.Add(collector.intArray[j + 1]);

                            j++;
                        }
                    }
                    else if (cmd == 'g')
                    {
                        //ProcessDeepLinkManager.Instance.Log(sb.ToString());
                    }
                }
            }

            var mesh = collector.ToMesh();

            return mesh;
        }

        private static float GetFloat(StringBuilder sb, ref int start, ref StringBuilder sbFloat)
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

        private static int GetInt(StringBuilder sb, ref int start, ref StringBuilder sbInt)
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

        private static float ParseFloat(StringBuilder value)
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

        private static int IntParseFast(StringBuilder value)
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
}