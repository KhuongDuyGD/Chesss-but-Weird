using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Chessboard : MonoBehaviour
{
    [Header("Art stuff")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private Material hoverMaterial;
    [SerializeField] private Color lightTileColor = new Color(0.72f, 0.64f, 0.50f, 1f);
    [SerializeField] private Color darkTileColor = new Color(0.24f, 0.28f, 0.30f, 1f);
    [SerializeField] private Color legalMoveTileColor = new Color(0.15f, 0.85f, 0.45f, 0.35f);
    [Tooltip("Visible hover quad offset above the detected board surface.")]
    [SerializeField] private float hoverDisplayHeightOffset = 0.001f;
    [Tooltip("Invisible raycast collider offset above the detected board surface.")]
    [SerializeField] private float tileRaycastHeightOffset = 0.02f;
    [SerializeField] private float tileColliderHeight = 0.06f;
    [Tooltip("Scales only the visible generated hover/highlight quad. The logical tile center stays unchanged.")]
    [SerializeField] private Vector2 tileVisualSizeMultiplier = Vector2.one;
    [Tooltip("Scales the invisible raycast box. Keep this near 1 to avoid excessive overlap between neighbor tiles.")]
    [SerializeField] private Vector2 tileColliderSizeMultiplier = new Vector2(1.0f, 1.0f);
    [SerializeField] private bool showGeneratedTiles;

    [Header("Logical board layout")]
    [Tooltip("Local corner and surface height of the 8x8 grid, independent of cosmetic meshes.")]
    [SerializeField] private Vector3 boardOrigin = Vector3.zero;
    [SerializeField] private Vector2 tileSize = Vector2.one;

    [SerializeField] private bool drawBoardSyncGizmos = true;

    private const int TILE_COUNT_X = 8;
    private const int TILE_COUNT_Y = 8;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    private GameObject[,] tiles;
    private MeshRenderer[,] tileRenderers;
    private Material[,] baseTileMaterials;
    private Material lightTileMaterial;
    private Material darkTileMaterial;
    private Material legalMoveTileMaterial;
    private Camera currentCamera;
    private Vector2Int currentHover = -Vector2Int.one;
    private bool[,] legalMoveHighlights;
    private bool interactionEnabled;
    private bool presentationVisible = true;
    private Transform cosmeticBoard;
    // Measured centers/surface of the six gold pads in Tazji's arena (board units).
    [Header("Prisoner Rails")]
    [SerializeField] private float prisonRailX = 4.93f;
    [SerializeField] private float prisonFirstRowZ = 3.15f;
    [SerializeField] private float prisonRowSpacing = 1.26f;
    [SerializeField] private float prisonSurfaceY = .036f;
    public Vector3 GetPrisonSlotWorld(PieceTeam capturer, int slot)
    {
        float side = capturer == PieceTeam.White ? -1f : 1f;
        var local = new Vector3(side * prisonRailX, prisonSurfaceY, -side * (prisonFirstRowZ - slot * prisonRowSpacing));
        if (cosmeticBoard) return cosmeticBoard.TransformPoint(local);
        return GetBoardCenterWorld() + TransformBoardLocalOffset(new Vector3(local.x * currentBoardLayout.tileWidth, local.y, local.z * currentBoardLayout.tileDepth));
    }
    private bool generatedTilesBeforeCosmetic;

    // Board prefabs use an 8x8 playable area centered at origin, surface at local Y=0.
    public void AttachCosmeticBoard(Transform visual)
    {
        if (!cosmeticBoard) generatedTilesBeforeCosmetic = showGeneratedTiles;
        cosmeticBoard = visual;
        if (!visual) return;
        showGeneratedTiles = false;
        RefreshAllTileVisuals();
        visual.SetParent(transform, false);
        visual.position = GetBoardCenterWorld();
        visual.localRotation = Quaternion.identity;
        visual.localScale = new Vector3(currentBoardLayout.tileWidth, 1, currentBoardLayout.tileDepth);
        ApplyBoardRendererVisibility();
    }

    public void DetachCosmeticBoard()
    {
        if (cosmeticBoard) showGeneratedTiles = generatedTilesBeforeCosmetic;
        cosmeticBoard = null;
        RefreshAllTileVisuals();
        ApplyBoardRendererVisibility();
    }

    public void ShowFallbackBoard()
    {
        showGeneratedTiles = true;
        RefreshAllTileVisuals();
    }
    private int tileLayer;
    private int hoverLayer;
    private int hoverRaycastMask;
    private BoardLayout currentBoardLayout;
    private readonly Dictionary<GameObject, Vector2Int> tileLookup = new Dictionary<GameObject, Vector2Int>(TILE_COUNT_X * TILE_COUNT_Y);

    private void Awake()
    {
        tileLayer = LayerMask.NameToLayer("Tile");
        hoverLayer = LayerMask.NameToLayer("Hover");
        hoverRaycastMask = CreateLayerMaskOrDefault("Tile", "Hover");

        CreateTileMaterials();
        currentBoardLayout = ResolveBoardLayout();
        GenerateAllTiles(currentBoardLayout, TILE_COUNT_X, TILE_COUNT_Y);
    }

    private void OnDestroy()
    {
        DestroyRuntimeMaterial(lightTileMaterial);
        DestroyRuntimeMaterial(darkTileMaterial);
        DestroyRuntimeMaterial(legalMoveTileMaterial);
    }

    private void Update()
    {
        if (!interactionEnabled)
        {
            ClearCurrentHover();
            return;
        }

        if (!currentCamera)
            currentCamera = Camera.main;

        if (!currentCamera)
            return;

        if (Mouse.current == null)
        {
            ClearCurrentHover();
            return;
        }

        Ray ray = currentCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit info, 100, hoverRaycastMask))
        {
            Vector2Int hitPosition = LookupTileIndex(info.transform.gameObject);
            if (!IsValidTilePosition(hitPosition))
            {
                ClearCurrentHover();
                return;
            }

            if (currentHover == hitPosition)
                return;

            ClearCurrentHover();
            currentHover = hitPosition;
            SetTileHover(hitPosition, true);
        }
        else
        {
            ClearCurrentHover();
        }
    }

    private void GenerateAllTiles(BoardLayout boardLayout, int tileCountX, int tileCountY)
    {
        tiles = new GameObject[tileCountX, tileCountY];
        tileRenderers = new MeshRenderer[tileCountX, tileCountY];
        baseTileMaterials = new Material[tileCountX, tileCountY];
        legalMoveHighlights = new bool[tileCountX, tileCountY];
        tileLookup.Clear();

        for (int x = 0; x < tileCountX; x++)
            for (int y = 0; y < tileCountY; y++)
            {
                tiles[x, y] = GenerateSingleTile(boardLayout, x, y);
                tileLookup[tiles[x, y]] = new Vector2Int(x, y);
                tileRenderers[x, y] = tiles[x, y].GetComponent<MeshRenderer>();
                baseTileMaterials[x, y] = (x + y) % 2 == 0 ? lightTileMaterial : darkTileMaterial;
                tileRenderers[x, y].sharedMaterial = baseTileMaterials[x, y];
                tileRenderers[x, y].enabled = showGeneratedTiles;
            }
    }

    private GameObject GenerateSingleTile(BoardLayout boardLayout, int x, int y)
    {
        GameObject tileObject = new GameObject(string.Format("X:{0}, Y:{1}", x, y));
        tileObject.transform.parent = transform;
        tileObject.transform.localPosition = new Vector3(
            boardLayout.origin.x + x * boardLayout.tileWidth,
            boardLayout.colliderY,
            boardLayout.origin.z + y * boardLayout.tileDepth);

        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>();

        float visualLocalY = boardLayout.visualY - boardLayout.colliderY;

        Vector2 visualSize = GetHoverVisualTileSize(boardLayout);
        float visualPaddingX = (visualSize.x - boardLayout.tileWidth) * 0.5f;
        float visualPaddingZ = (visualSize.y - boardLayout.tileDepth) * 0.5f;

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(-visualPaddingX, visualLocalY, -visualPaddingZ);
        vertices[1] = new Vector3(-visualPaddingX, visualLocalY, boardLayout.tileDepth + visualPaddingZ);
        vertices[2] = new Vector3(boardLayout.tileWidth + visualPaddingX, visualLocalY, -visualPaddingZ);
        vertices[3] = new Vector3(boardLayout.tileWidth + visualPaddingX, visualLocalY, boardLayout.tileDepth + visualPaddingZ);

        int[] tris = new int[] { 0, 1, 2, 1, 3, 2 };

        mesh.vertices = vertices;
        mesh.triangles = tris;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        tileObject.layer = tileLayer;

        BoxCollider tileCollider = tileObject.AddComponent<BoxCollider>();
        Vector2 colliderSize = GetTileColliderSize(boardLayout);
        tileCollider.center = new Vector3(boardLayout.tileWidth * 0.5f, 0, boardLayout.tileDepth * 0.5f);
        tileCollider.size = new Vector3(colliderSize.x, Mathf.Max(0.001f, tileColliderHeight), colliderSize.y);

        return tileObject;
    }

    private void SetTileHover(Vector2Int position, bool isHovering)
    {
        if (!IsValidTilePosition(position))
            return;

        tiles[position.x, position.y].layer = isHovering ? hoverLayer : tileLayer;
        RefreshTileVisual(position);
    }

    private void ClearCurrentHover()
    {
        if (currentHover == -Vector2Int.one)
            return;

        Vector2Int previousHover = currentHover;
        currentHover = -Vector2Int.one;
        SetTileHover(previousHover, false);
    }

    public void SetLegalMoveHighlights(IEnumerable<Vector2Int> positions)
    {
        ClearLegalMoveHighlights();

        if (positions == null)
            return;

        foreach (Vector2Int position in positions)
        {
            if (!IsValidTilePosition(position))
                continue;

            legalMoveHighlights[position.x, position.y] = true;
            RefreshTileVisual(position);
        }
    }

    public void ClearLegalMoveHighlights()
    {
        if (legalMoveHighlights == null)
            return;

        for (int x = 0; x < legalMoveHighlights.GetLength(0); x++)
            for (int y = 0; y < legalMoveHighlights.GetLength(1); y++)
            {
                if (!legalMoveHighlights[x, y])
                    continue;

                legalMoveHighlights[x, y] = false;
                RefreshTileVisual(new Vector2Int(x, y));
            }
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        if (interactionEnabled)
            return;

        ClearCurrentHover();
        ClearLegalMoveHighlights();
    }

    public void SetPresentationVisible(bool visible)
    {
        presentationVisible = visible;
        ApplyBoardRendererVisibility();
        RefreshAllTileVisuals();
    }

    private void RefreshTileVisual(Vector2Int position)
    {
        if (!IsValidTilePosition(position))
            return;

        bool isHovering = currentHover == position;
        bool isLegalMove = legalMoveHighlights != null && legalMoveHighlights[position.x, position.y];
        tileRenderers[position.x, position.y].enabled = presentationVisible && (showGeneratedTiles || isHovering || isLegalMove);

        if (isHovering && hoverMaterial)
            tileRenderers[position.x, position.y].sharedMaterial = hoverMaterial;
        else if (isLegalMove && legalMoveTileMaterial)
            tileRenderers[position.x, position.y].sharedMaterial = legalMoveTileMaterial;
        else
            tileRenderers[position.x, position.y].sharedMaterial = baseTileMaterials[position.x, position.y];
    }

    private void RefreshAllTileVisuals()
    {
        if (tileRenderers == null)
            return;

        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                RefreshTileVisual(new Vector2Int(x, y));
    }

    private void ApplyBoardRendererVisibility()
    {
        if (cosmeticBoard)
            foreach (var renderer in cosmeticBoard.GetComponentsInChildren<Renderer>(true)) renderer.enabled = presentationVisible;
    }

    public Vector3 GetTileCenterWorld(Vector2Int tile)
    {
        if (!IsValidTile(tile))
            return transform.position;

        Vector3 localCenter = new Vector3(
            currentBoardLayout.origin.x + (tile.x + 0.5f) * currentBoardLayout.tileWidth,
            currentBoardLayout.surfaceY,
            currentBoardLayout.origin.z + (tile.y + 0.5f) * currentBoardLayout.tileDepth);

        return transform.TransformPoint(localCenter);
    }

    public Vector3 TransformBoardLocalOffset(Vector3 localOffset)
    {
        return transform.TransformVector(localOffset);
    }

    public Vector3 GetBoardCenterWorld()
    {
        Vector3 localCenter = new Vector3(
            currentBoardLayout.origin.x + currentBoardLayout.tileWidth * 4f,
            currentBoardLayout.surfaceY,
            currentBoardLayout.origin.z + currentBoardLayout.tileDepth * 4f);
        return transform.TransformPoint(localCenter);
    }

    public float GetBoardSurfaceY()
    {
        return transform.TransformPoint(new Vector3(0f, currentBoardLayout.surfaceY, 0f)).y;
    }

    public bool TryGetTileFromObject(GameObject tileObject, out Vector2Int tile)
    {
        tile = LookupTileIndex(tileObject);
        return IsValidTile(tile);
    }

    public bool IsValidTile(Vector2Int tile)
    {
        return IsValidTilePosition(tile);
    }

    private Vector2Int LookupTileIndex(GameObject hitInfo)
    {
        if (hitInfo && tileLookup.TryGetValue(hitInfo, out Vector2Int tile))
            return tile;

        return -Vector2Int.one;
    }

    private static int CreateLayerMaskOrDefault(params string[] layerNames)
    {
        int mask = LayerMask.GetMask(layerNames);
        return mask == 0 ? Physics.DefaultRaycastLayers : mask;
    }

    private bool IsValidTilePosition(Vector2Int position)
    {
        return position.x >= 0 &&
            position.x < TILE_COUNT_X &&
            position.y >= 0 &&
            position.y < TILE_COUNT_Y;
    }

    private void CreateTileMaterials()
    {
        lightTileMaterial = CreateTileMaterial("Light Chess Tile", lightTileColor);
        darkTileMaterial = CreateTileMaterial("Dark Chess Tile", darkTileColor);
        legalMoveTileMaterial = CreateTileMaterial("Legal Move Chess Tile", legalMoveTileColor, true);
    }

    private BoardLayout ResolveBoardLayout()
    {
        return new BoardLayout
        {
            origin = boardOrigin,
            surfaceY = boardOrigin.y,
            visualY = boardOrigin.y + hoverDisplayHeightOffset,
            colliderY = boardOrigin.y + tileRaycastHeightOffset,
            tileWidth = Mathf.Max(.01f, tileSize.x),
            tileDepth = Mathf.Max(.01f, tileSize.y)
        };
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawBoardSyncGizmos)
            return;

        BoardLayout boardLayout = ResolveBoardLayout();
        float width = boardLayout.tileWidth * TILE_COUNT_X;
        float depth = boardLayout.tileDepth * TILE_COUNT_Y;
        Vector3 origin = new Vector3(boardLayout.origin.x, boardLayout.visualY, boardLayout.origin.z);

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        for (int x = 0; x <= TILE_COUNT_X; x++)
        {
            float localX = origin.x + x * boardLayout.tileWidth;
            Gizmos.DrawLine(new Vector3(localX, origin.y, origin.z), new Vector3(localX, origin.y, origin.z + depth));
        }

        for (int y = 0; y <= TILE_COUNT_Y; y++)
        {
            float localZ = origin.z + y * boardLayout.tileDepth;
            Gizmos.DrawLine(new Vector3(origin.x, origin.y, localZ), new Vector3(origin.x + width, origin.y, localZ));
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            new Vector3(origin.x + width * 0.5f, boardLayout.colliderY, origin.z + depth * 0.5f),
            new Vector3(width, Mathf.Max(0.001f, tileColliderHeight), depth));

        Vector2 hoverSize = GetHoverVisualTileSize(boardLayout);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(
            new Vector3(origin.x + width * 0.5f, boardLayout.visualY, origin.z + depth * 0.5f),
            new Vector3(hoverSize.x * TILE_COUNT_X, 0.01f, hoverSize.y * TILE_COUNT_Y));
        Gizmos.matrix = Matrix4x4.identity;
    }

    private Vector2 GetScaledTileSize(BoardLayout boardLayout, Vector2 multiplier)
    {
        float xMultiplier = Mathf.Max(0.01f, multiplier.x);
        float yMultiplier = Mathf.Max(0.01f, multiplier.y);
        return new Vector2(boardLayout.tileWidth * xMultiplier, boardLayout.tileDepth * yMultiplier);
    }

    private Vector2 GetHoverVisualTileSize(BoardLayout boardLayout)
    {
        return GetScaledTileSize(boardLayout, tileVisualSizeMultiplier);
    }

    private Vector2 GetTileColliderSize(BoardLayout boardLayout)
    {
        return GetScaledTileSize(boardLayout, tileColliderSizeMultiplier);
    }

    private Material CreateTileMaterial(string materialName, Color color, bool transparent = false)
    {
        Shader shader = tileMaterial ? tileMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
        if (!shader)
            shader = Shader.Find("Standard");

        Material material = new Material(shader)
        {
            name = materialName,
            renderQueue = transparent
                ? (int)UnityEngine.Rendering.RenderQueue.Transparent
                : -1
        };

        if (transparent)
            ConfigureTransparentMaterial(material);
        else
            ConfigureOpaqueMaterial(material);

        SetMaterialColor(material, color);
        ClearBaseTexture(material);

        return material;
    }

    private void ConfigureOpaqueMaterial(Material material)
    {
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 0);

        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 1);

        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);

        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);

        material.SetOverrideTag("RenderType", "Opaque");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHABLEND_ON");
    }

    private void ConfigureTransparentMaterial(Material material)
    {
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1);

        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 3);

        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0);

        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    private void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty(BaseColorId))
            material.SetColor(BaseColorId, color);

        if (material.HasProperty(ColorId))
            material.SetColor(ColorId, color);
    }

    private void ClearBaseTexture(Material material)
    {
        if (material.HasProperty(BaseMapId))
            material.SetTexture(BaseMapId, null);

        if (material.HasProperty(MainTexId))
            material.SetTexture(MainTexId, null);
    }

    private void DestroyRuntimeMaterial(Material material)
    {
        if (!material)
            return;

        if (Application.isPlaying)
            Destroy(material);
        else
            DestroyImmediate(material);
    }

    private struct BoardLayout
    {
        public Vector3 origin;
        public float surfaceY;
        public float visualY;
        public float colliderY;
        public float tileWidth;
        public float tileDepth;
    }

}
