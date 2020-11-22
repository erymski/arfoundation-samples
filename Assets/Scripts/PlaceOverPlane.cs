using System;
using System.Collections.Generic;
using Assets.Scripts;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class PlaceOverPlane : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Instantiates this prefab on a plane at the touch location.")]
    private GameObject m_PlacedPrefab;

    /// <summary>
    /// The prefab to instantiate on touch.
    /// </summary>
    public GameObject placedPrefab
    {
        get { return m_PlacedPrefab; }
        set { m_PlacedPrefab = value; }
    }

    private ARRaycastManager _raycastManager;

    /// <summary>
    /// The object instantiated as a result of a successful raycast intersection with a plane.
    /// </summary>
    private GameObject SpawnedObject { get; set; }

    /// <summary>
    /// Bound for the prefab.
    /// </summary>
    private Bounds Bounds
    {
        get
        {
            while (! _bounds.HasValue)
            {
                LogMessage("Calculating bounds");

                var meshFilters = Template.GetComponentsInChildren<MeshFilter>();
                if (meshFilters == null)
                {
                    LogMessage("Cannot detect meshes in the prefab");
                    break;
                }

                LogMessage($"***** Meshes count: {meshFilters.Length}");
                if (meshFilters.Length == 0) break;

                var mesh = meshFilters[0].mesh;

                var firstBound = mesh.bounds;
                LogMessage($"First bound: {firstBound}");
                var bounds = new Bounds(firstBound.center, firstBound.size);

                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    var sub = mesh.GetSubMesh(i);
//                    LogMessage($"sub bound: {sub.bounds}");

                    bounds.Encapsulate(sub.bounds);
                }

                //for (int i = 1; i < meshFilters.Length; i++)
                //{
                //    LogMessage($"{i} bound: {meshFilters[i].mesh.bounds}");
                //    bounds.Encapsulate(meshFilters[i].mesh.bounds);
                //}

                LogMessage($"**** BOUNDS: {bounds}");
                _bounds = bounds;
                break;
            }

            return _bounds.GetValueOrDefault();
        }
    }


    private Bounds? _bounds;

    /// <summary>
    /// Template to be instantiated on touch.
    /// </summary>
    private GameObject Template
    {
        get
        {
            if (_customTemplate) return _customTemplate;

            try
            {
                var manager = ProcessDeepLinkManager.Instance;
                var meshes = manager.LoadedMeshes;
                if (meshes == null)
                {
                    LogMessage("Use default prefab");
                    return m_PlacedPrefab;
                }

                manager.Log($"Creating a new template for {meshes.Length} meshes");

                _customTemplate = new GameObject("LD2020 template");

                CombineInstance[] combined = new CombineInstance[meshes.Length];

                for (int i = 0; i < meshes.Length; i++)
                {
                    combined[i].mesh = meshes[i];
                    LogMessage($"New mesh with {meshes[i].bounds}");
                    //var meshFilter = _customTemplate.AddComponent<MeshFilter>();
                    //LogMessage($"Filter exists is {meshFilter != null}");
                    //meshFilter.sharedMesh = meshes[i];
                }
                var meshFilter = _customTemplate.AddComponent<MeshFilter>();
                meshFilter.mesh = new Mesh();
                meshFilter.mesh.CombineMeshes(combined, false);
                meshFilter.mesh.RecalculateBounds();

                LogMessage("Create new mesh renderer");
                var meshRenderer = _customTemplate.AddComponent<MeshRenderer>();
                meshRenderer.material = new Material(Shader.Find("Standard"));

                _bounds = null;
            }
            catch (Exception e)
            {
                LogMessage($"Failed with {e}");
                throw;
            }

            return _customTemplate;
        }
    }

    private GameObject _customTemplate;

    void Awake()
    {
        _raycastManager = GetComponent<ARRaycastManager>();
        Application.targetFrameRate = 60;
    }

    bool TryGetTouchPosition(out Vector2 touchPosition)
    {
#if UNITY_EDITOR
        if (Input.GetMouseButton(0))
        {
            var mousePosition = Input.mousePosition;
            touchPosition = new Vector2(mousePosition.x, mousePosition.y);
            return true;
        }
#else
        if (Input.touchCount > 0)
        {
            touchPosition = Input.GetTouch(0).position;
            return true;
        }
#endif

        touchPosition = default;
        return false;
    }

    void Update()
    {
        if (!TryGetTouchPosition(out Vector2 touchPosition))
            return;

        var hits = new List<ARRaycastHit>();
        if (_raycastManager.Raycast(touchPosition, hits, TrackableType.PlaneWithinPolygon))
        {
            // Raycast hits are sorted by distance, so the first one
            // will be the closest hit.
            Pose hitPose = hits[0].pose;

            // origin is at the center, so move position in Y+ direction to place object over the plane
            var hitPosition = hitPose.position;
            hitPosition.y += (Bounds.size.y / 2);

            if (SpawnedObject == null)
            {
                LogMessage("Placing new object");
                SpawnedObject = Instantiate(Template, hitPosition, hitPose.rotation);
            }
            else
            {
                LogMessage("Moving existing object");
                SpawnedObject.transform.position = hitPosition;
            }
        }
    }

    private static void LogMessage(string message) => ProcessDeepLinkManager.Instance.Log(message);
}
