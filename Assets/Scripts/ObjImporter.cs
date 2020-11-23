using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.Scripts
{
    public sealed class ObjImporter
    {
        #region Inner types

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

        private class MeshCollector
        {
            public readonly List<int> triangles = new List<int>();
            public readonly List<Vector3Int> faceData = new List<Vector3Int>();

            public bool IsEmpty => triangles.Count == 0;

            public Mesh ToMesh(List<Vector3> vertices, List<Vector2> uv, List<Vector3> normals)
            {
                if (IsEmpty) throw new Exception("Empty mesh");

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


                mesh.Optimize();
                mesh.RecalculateBounds();

//                LogMessage($"Calculated {mesh.bounds}");

                return mesh;
            }
        }

        #endregion

        public static Mesh[] Process(string objContent)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();
            List<int> intArray = new List<int>();

            var collectors = new List<MeshCollector>();
            MeshCollector collector = null;

            var sb = new StringBuilder();

            int start = 0;
            string objectName = null;
            int faceDataCount = 0;

            StringBuilder sbFloat = new StringBuilder();

            for (int i = 0; i < objContent.Length; i++)
            {
                if (objContent[i] == '\n')
                {
                    sb.Remove(0, sb.Length);

                    // Start +1 for whitespace '\n'
                    sb.Append(objContent, start + 1, i - start);
                    start = i;

                    var cmd = sb[0];
                    if (cmd == 'g') // like `g cp4970-125pf-2-solid1`.  Name of a solid I guess.
                    {
                        collector = new MeshCollector();
                        collectors.Add(collector);
                        faceDataCount = 0;
                    }
                    else if (cmd == 'o' && sb[1] == ' ')
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

                        vertices.Add(new Vector3(GetFloat(sb, ref splitStart, ref sbFloat),
                            GetFloat(sb, ref splitStart, ref sbFloat), GetFloat(sb, ref splitStart, ref sbFloat)));
                    }
                    else if (cmd == 'v' && sb[1] == 't' && sb[2] == ' ') // UV
                    {
                        int splitStart = 3;

                        uv.Add(new Vector2(GetFloat(sb, ref splitStart, ref sbFloat),
                            GetFloat(sb, ref splitStart, ref sbFloat)));
                    }
                    else if (cmd == 'v' && sb[1] == 'n' && sb[2] == ' ') // Normals
                    {
                        int splitStart = 3;

                        normals.Add(new Vector3(GetFloat(sb, ref splitStart, ref sbFloat),
                            GetFloat(sb, ref splitStart, ref sbFloat), GetFloat(sb, ref splitStart, ref sbFloat)));
                    }
                    else if (cmd == 'f' && sb[1] == ' ')
                    {
                        int splitStart = 2;

                        int j = 1;
                        intArray.Clear();
                        int info = 0;
                        // Add faceData, a face can contain multiple triangles, facedata is stored in following order vert, uv, normal. If uv or normal are / set it to a 0
                        while (splitStart < sb.Length && char.IsDigit(sb[splitStart]))
                        {
                            collector.faceData.Add(new Vector3Int(GetInt(sb, ref splitStart, ref sbFloat),
                                GetInt(sb, ref splitStart, ref sbFloat), GetInt(sb, ref splitStart, ref sbFloat)));
                            j++;

                            intArray.Add(faceDataCount);
                            faceDataCount++;
                        }

                        info += j;
                        j = 1;
                        while (j + 2 < info) //Create triangles out of the face data.  There will generally be more than 1 triangle per face.
                        {
                            collector.triangles.Add(intArray[0]);
                            collector.triangles.Add(intArray[j]);
                            collector.triangles.Add(intArray[j + 1]);

                            j++;
                        }
                    }
                }
            }

            LogMessage($"Collected {collectors.Count} meshes");

            return collectors.Where(c => ! c.IsEmpty).Select(c => c.ToMesh(vertices, uv, normals)).ToArray();
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

            return float.Parse(sbFloat.ToString());
        }

        private static int GetInt(StringBuilder sb, ref int start, ref StringBuilder sbInt)
        {
            sbInt.Remove(0, sbInt.Length);
            while (start < sb.Length && char.IsDigit(sb[start]))
            {
                sbInt.Append(sb[start]);
                start++;
            }
            start++;

            return int.Parse(sbInt.ToString());
        }

        private static void LogMessage(string message) => ProcessDeepLinkManager.Instance.Log(message);
    }
}