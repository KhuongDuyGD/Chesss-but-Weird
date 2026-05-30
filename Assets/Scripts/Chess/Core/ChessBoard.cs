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
    [SerializeField] private bool showGeneratedTiles;

    [Header("Visual board sync")]
    [SerializeField] private Transform visualBoardRoot;
    [SerializeField] private string visualBoardObjectName = "ChessBoard_Scene";
    [SerializeField] private string visualBoardMaterialName = "ChessBoard_Final.001";
    [SerializeField, Range(0f, 0.45f)] private float playableAreaInsetPercent = 0.025f;
    [SerializeField] private Vector4 playableAreaInsetsPercent = new Vector4(0.16f, 0.13f, 0.14f, 0.14f);
    [SerializeField] private Vector2 playableAreaPositionOffset;
    [SerializeField] private Vector2 playableAreaSizeOffset;
    [Tooltip("Fits the virtual board from known visual piece positions. Use this when visual board bounds are correct but hover tiles still drift.")]
    [SerializeField] private bool useEstimatedPieceAnchors = true;
    [Tooltip("Optional overrides. Leave empty to use Rook_2/Bishop_2/Bishop_1/Rook_1 anchors from the imported visual set.")]
    [SerializeField] private PieceAnchor[] customEstimatedPieceAnchors;
    [SerializeField] private bool useManualPlayableArea;
    [Tooltip("Manual local-space lower-left playable corner. X/Z are board plane, Y is board surface height.")]
    [SerializeField] private Vector3 manualPlayableOrigin;
    [Tooltip("Manual local-space playable width/depth across all 8x8 tiles.")]
    [SerializeField] private Vector2 manualPlayableSize = new Vector2(8f, 8f);
    [SerializeField] private bool drawBoardSyncGizmos = true;

    private const int TILE_COUNT_X = 8;
    private const int TILE_COUNT_Y = 8;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly PieceAnchor[] DefaultEstimatedPieceAnchors =
    {
        new PieceAnchor("Rook_2", new Vector2Int(0, 7)),
        new PieceAnchor("Bishop_2", new Vector2Int(2, 7)),
        new PieceAnchor("Bishop_1", new Vector2Int(5, 0)),
        new PieceAnchor("Rook_1", new Vector2Int(7, 0))
    };

    private GameObject[,] tiles;
    private MeshRenderer[,] tileRenderers;
    private Material[,] baseTileMaterials;
    private Material lightTileMaterial;
    private Material darkTileMaterial;
    private Material legalMoveTileMaterial;
    private Camera currentCamera;
    private Vector2Int currentHover = -Vector2Int.one;
    private bool[,] legalMoveHighlights;
    private int tileLayer;
    private int hoverLayer;
    private BoardLayout currentBoardLayout;

    private void Awake()
    {
        tileLayer = LayerMask.NameToLayer("Tile");
        hoverLayer = LayerMask.NameToLayer("Hover");

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
        if (!currentCamera)
        {
            currentCamera = Camera.main;
            return;
        }

        if (Mouse.current == null)
        {
            ClearCurrentHover();
            return;
        }

        Ray ray = currentCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit info, 100, LayerMask.GetMask("Tile", "Hover")))
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

        for (int x = 0; x < tileCountX; x++)
            for (int y = 0; y < tileCountY; y++)
            {
                tiles[x, y] = GenerateSingleTile(boardLayout, x, y);
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
        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(0, visualLocalY, 0);
        vertices[1] = new Vector3(0, visualLocalY, boardLayout.tileDepth);
        vertices[2] = new Vector3(boardLayout.tileWidth, visualLocalY, 0);
        vertices[3] = new Vector3(boardLayout.tileWidth, visualLocalY, boardLayout.tileDepth);

        int[] tris = new int[] { 0, 1, 2, 1, 3, 2 };

        mesh.vertices = vertices;
        mesh.triangles = tris;

        mesh.RecalculateNormals();

        tileObject.layer = tileLayer;

        BoxCollider tileCollider = tileObject.AddComponent<BoxCollider>();
        tileCollider.center = new Vector3(boardLayout.tileWidth * 0.5f, 0, boardLayout.tileDepth * 0.5f);
        tileCollider.size = new Vector3(boardLayout.tileWidth, Mathf.Max(0.001f, tileColliderHeight), boardLayout.tileDepth);

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

    private void RefreshTileVisual(Vector2Int position)
    {
        if (!IsValidTilePosition(position))
            return;

        bool isHovering = currentHover == position;
        bool isLegalMove = legalMoveHighlights != null && legalMoveHighlights[position.x, position.y];
        tileRenderers[position.x, position.y].enabled = showGeneratedTiles || isHovering || isLegalMove;

        if (isHovering && hoverMaterial)
            tileRenderers[position.x, position.y].sharedMaterial = hoverMaterial;
        else if (isLegalMove && legalMoveTileMaterial)
            tileRenderers[position.x, position.y].sharedMaterial = legalMoveTileMaterial;
        else
            tileRenderers[position.x, position.y].sharedMaterial = baseTileMaterials[position.x, position.y];
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
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (tiles[x, y] == hitInfo)
                    return new Vector2Int(x, y);

        return -Vector2Int.one;
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
        if (useManualPlayableArea)
            return CreateManualBoardLayout();

        if (useEstimatedPieceAnchors && TryCreateEstimatedPieceAnchorLayout(out BoardLayout estimatedLayout))
            return estimatedLayout;

        if (TryGetVisualBoardBounds(out Bounds boardBounds))
        {
            Vector4 resolvedInsets = GetResolvedPlayableAreaInsets();
            float leftInset = boardBounds.size.x * resolvedInsets.x;
            float rightInset = boardBounds.size.x * resolvedInsets.y;
            float bottomInset = boardBounds.size.z * resolvedInsets.z;
            float topInset = boardBounds.size.z * resolvedInsets.w;
            float playableWidth = Mathf.Max(0.01f, boardBounds.size.x - leftInset - rightInset + playableAreaSizeOffset.x);
            float playableDepth = Mathf.Max(0.01f, boardBounds.size.z - bottomInset - topInset + playableAreaSizeOffset.y);
            float surfaceY = boardBounds.max.y;

            return new BoardLayout
            {
                origin = new Vector3(
                    boardBounds.min.x + leftInset + playableAreaPositionOffset.x,
                    0,
                    boardBounds.min.z + bottomInset + playableAreaPositionOffset.y),
                surfaceY = surfaceY,
                visualY = surfaceY + hoverDisplayHeightOffset,
                colliderY = surfaceY + tileRaycastHeightOffset,
                tileWidth = playableWidth / TILE_COUNT_X,
                tileDepth = playableDepth / TILE_COUNT_Y
            };
        }

        return new BoardLayout
        {
            origin = Vector3.zero,
            surfaceY = 0f,
            visualY = hoverDisplayHeightOffset,
            colliderY = tileRaycastHeightOffset,
            tileWidth = 1f,
            tileDepth = 1f
        };
    }

    private bool TryCreateEstimatedPieceAnchorLayout(out BoardLayout boardLayout)
    {
        boardLayout = default;
        PieceAnchor[] anchors = customEstimatedPieceAnchors != null && customEstimatedPieceAnchors.Length > 0
            ? customEstimatedPieceAnchors
            : DefaultEstimatedPieceAnchors;

        if (!TryFitAxis(anchors, true, out float originX, out float tileWidth) ||
            !TryFitAxis(anchors, false, out float originZ, out float tileDepth))
            return false;

        float surfaceY = 0f;
        if (TryGetVisualBoardBounds(out Bounds boardBounds))
            surfaceY = boardBounds.max.y;

        boardLayout = new BoardLayout
        {
            origin = new Vector3(originX + playableAreaPositionOffset.x, surfaceY, originZ + playableAreaPositionOffset.y),
            surfaceY = surfaceY,
            visualY = surfaceY + hoverDisplayHeightOffset,
            colliderY = surfaceY + tileRaycastHeightOffset,
            tileWidth = Mathf.Max(0.01f, (tileWidth * TILE_COUNT_X + playableAreaSizeOffset.x) / TILE_COUNT_X),
            tileDepth = Mathf.Max(0.01f, (tileDepth * TILE_COUNT_Y + playableAreaSizeOffset.y) / TILE_COUNT_Y)
        };

        return true;
    }

    private bool TryFitAxis(PieceAnchor[] anchors, bool useX, out float origin, out float tileSize)
    {
        origin = 0f;
        tileSize = 0f;

        float sumIndex = 0f;
        float sumPosition = 0f;
        float sumIndexPosition = 0f;
        float sumIndexSquared = 0f;
        int validAnchorCount = 0;

        for (int i = 0; i < anchors.Length; i++)
        {
            if (!TryGetAnchorLocalPosition(anchors[i], out Vector3 localPosition))
                continue;

            float index = useX ? anchors[i].boardPosition.x + 0.5f : anchors[i].boardPosition.y + 0.5f;
            float position = useX ? localPosition.x : localPosition.z;
            sumIndex += index;
            sumPosition += position;
            sumIndexPosition += index * position;
            sumIndexSquared += index * index;
            validAnchorCount++;
        }

        float denominator = validAnchorCount * sumIndexSquared - sumIndex * sumIndex;
        if (validAnchorCount < 2 || Mathf.Abs(denominator) < 0.0001f)
            return false;

        tileSize = (validAnchorCount * sumIndexPosition - sumIndex * sumPosition) / denominator;
        origin = (sumPosition - tileSize * sumIndex) / validAnchorCount;

        if (tileSize < 0f)
        {
            tileSize = Mathf.Abs(tileSize);
            origin -= tileSize * TILE_COUNT_X;
        }

        return true;
    }

    private bool TryGetAnchorLocalPosition(PieceAnchor anchor, out Vector3 localPosition)
    {
        Transform anchorTransform = FindVisualChild(anchor.objectName);
        if (!anchorTransform)
        {
            localPosition = default;
            return false;
        }

        localPosition = transform.InverseTransformPoint(anchorTransform.position);
        return true;
    }

    private BoardLayout CreateManualBoardLayout()
    {
        float surfaceY = manualPlayableOrigin.y;
        return new BoardLayout
        {
            origin = manualPlayableOrigin,
            surfaceY = surfaceY,
            visualY = surfaceY + hoverDisplayHeightOffset,
            colliderY = surfaceY + tileRaycastHeightOffset,
            tileWidth = Mathf.Max(0.01f, manualPlayableSize.x) / TILE_COUNT_X,
            tileDepth = Mathf.Max(0.01f, manualPlayableSize.y) / TILE_COUNT_Y
        };
    }

    private Vector4 GetResolvedPlayableAreaInsets()
    {
        if (playableAreaInsetsPercent != Vector4.zero)
            return ClampInsetPercent(playableAreaInsetsPercent);

        return ClampInsetPercent(new Vector4(
            playableAreaInsetPercent,
            playableAreaInsetPercent,
            playableAreaInsetPercent,
            playableAreaInsetPercent));
    }

    private Vector4 ClampInsetPercent(Vector4 insets)
    {
        return new Vector4(
            Mathf.Clamp(insets.x, 0f, 0.45f),
            Mathf.Clamp(insets.y, 0f, 0.45f),
            Mathf.Clamp(insets.z, 0f, 0.45f),
            Mathf.Clamp(insets.w, 0f, 0.45f));
    }

    private bool TryGetVisualBoardBounds(out Bounds boardBounds)
    {
        Renderer[] renderers = GetVisualBoardSearchRoot().GetComponentsInChildren<Renderer>(true);
        bool foundBounds = false;
        boardBounds = new Bounds();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer currentRenderer = renderers[i];
            if (!IsVisualBoardRenderer(currentRenderer))
                continue;

            Bounds localBounds = WorldBoundsToLocalBounds(currentRenderer.bounds);
            if (!foundBounds)
            {
                boardBounds = localBounds;
                foundBounds = true;
            }
            else
            {
                boardBounds.Encapsulate(localBounds);
            }
        }

        return foundBounds;
    }

    private Transform GetVisualBoardSearchRoot()
    {
        if (visualBoardRoot)
            return visualBoardRoot;

        Transform foundRoot = transform.Find("VisualChessSet");
        return foundRoot ? foundRoot : transform;
    }

    private bool IsVisualBoardRenderer(Renderer currentRenderer)
    {
        if (!string.IsNullOrWhiteSpace(visualBoardObjectName) &&
            currentRenderer.gameObject.name.Contains(visualBoardObjectName))
            return true;

        Material[] sharedMaterials = currentRenderer.sharedMaterials;
        for (int i = 0; i < sharedMaterials.Length; i++)
        {
            Material sharedMaterial = sharedMaterials[i];
            if (sharedMaterial &&
                !string.IsNullOrWhiteSpace(visualBoardMaterialName) &&
                sharedMaterial.name.StartsWith(visualBoardMaterialName))
                return true;
        }

        return false;
    }

    private Transform FindVisualChild(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        Transform searchRoot = GetVisualBoardSearchRoot();
        Transform[] children = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            if (children[i].name == objectName)
                return children[i];

        return null;
    }

    private Bounds WorldBoundsToLocalBounds(Bounds worldBounds)
    {
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int x = 0; x <= 1; x++)
            for (int y = 0; y <= 1; y++)
                for (int z = 0; z <= 1; z++)
                {
                    Vector3 worldPoint = new Vector3(
                        x == 0 ? worldBounds.min.x : worldBounds.max.x,
                        y == 0 ? worldBounds.min.y : worldBounds.max.y,
                        z == 0 ? worldBounds.min.z : worldBounds.max.z);
                    Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
                    min = Vector3.Min(min, localPoint);
                    max = Vector3.Max(max, localPoint);
                }

        Bounds localBounds = new Bounds((min + max) * 0.5f, max - min);
        return localBounds;
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
        Gizmos.matrix = Matrix4x4.identity;
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

    [System.Serializable]
    private struct PieceAnchor
    {
        public string objectName;
        public Vector2Int boardPosition;

        public PieceAnchor(string objectName, Vector2Int boardPosition)
        {
            this.objectName = objectName;
            this.boardPosition = boardPosition;
        }
    }
}
