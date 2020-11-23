using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
            private readonly string _name;
            public readonly List<int> triangles = new List<int>();
            public readonly List<Vector3Int> faceData = new List<Vector3Int>();

            public MeshCollector(string name)
            {
                _name = name;
            }

            public bool IsEmpty => triangles.Count == 0;

            public Mesh ToMesh(IReadOnlyList<Vector3> vertices, IReadOnlyList<Vector2> uv, IReadOnlyList<Vector3> normals)
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
                    name = _name,
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

        public static Mesh[] Process(StreamReader reader)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var normals = new List<Vector3>();
            var intArray = new List<int>();

            var collectors = new List<MeshCollector>();
            MeshCollector collector = null;

            string objectName = null;
            int faceDataCount = 0;

            var buffer = new StringBuilder();

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var lineLength = line.Length;
                if (lineLength == 0) continue;

                var cmd = line[0];
                if (cmd == '#') continue;

                if (cmd == 'g') // like `g cp4970-125pf-2-solid1`.  Name of a solid I guess.
                {
                    collector = new MeshCollector(line.Substring(2));
                    collectors.Add(collector);
                    faceDataCount = 0;
                    continue;
                }

                if (cmd == 'o' && line[1] == ' ')
                {
                    int j = 2;
                    while (j < lineLength)
                    {
                        objectName += line[j];
                        j++;
                    }
                }
                else if (cmd == 'v' && line[1] == ' ') // Vertices
                {
                    int pos = 2;

                    vertices.Add(new Vector3(GetFloat(line, ref pos, ref buffer),
                                                GetFloat(line, ref pos, ref buffer),
                                                GetFloat(line, ref pos, ref buffer)));
                }
                else if (cmd == 'v' && line[1] == 't' && line[2] == ' ') // UV
                {
                    int pos = 3;

                    uv.Add(new Vector2(GetFloat(line, ref pos, ref buffer), GetFloat(line, ref pos, ref buffer)));
                }
                else if (cmd == 'v' && line[1] == 'n' && line[2] == ' ') // Normals
                {
                    int pos = 3;

                    normals.Add(new Vector3(GetFloat(line, ref pos, ref buffer),
                                            GetFloat(line, ref pos, ref buffer),
                                            GetFloat(line, ref pos, ref buffer)));
                }
                else if (cmd == 'f' && line[1] == ' ')
                {
                    int pos = 2;

                    int j = 1;
                    intArray.Clear();
                    int info = 0;
                    // Add faceData, a face can contain multiple triangles, facedata is stored in following order vert, uv, normal. If uv or normal are / set it to a 0
                    while (pos < lineLength && char.IsDigit(line[pos]))
                    {
                        collector.faceData.Add(new Vector3Int(GetInt(line, ref pos), GetInt(line, ref pos), GetInt(line, ref pos)));
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

            LogMessage($"Collected {collectors.Count} meshes");

            return collectors.Where(c => ! c.IsEmpty).Select(c => c.ToMesh(vertices, uv, normals)).ToArray();
        }

        private static float GetFloat(string line, ref int start, ref StringBuilder sbFloat)
        {
            sbFloat.Length = 0;
            while (start < line.Length &&
                   (char.IsDigit(line[start]) || line[start] == '-' || line[start] == '.'))
            {
                sbFloat.Append(line[start]);
                start++;
            }
            start++;

            return float.Parse(sbFloat.ToString(), CultureInfo.InvariantCulture);
        }

        private static int GetInt(string line, ref int pos)
        {
            int result = 0;
            while (pos < line.Length && char.IsDigit(line[pos]))
            {
                result = 10 * result + (line[pos] - 48);
                pos++;
            }

            pos++;

            return result;
        }

        private static void LogMessage(string message) => ProcessDeepLinkManager.Instance.Log(message);
    }
}