using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class Chessboard : MonoBehaviour
{
    [Header("Art stuff")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private Material hoverMaterial;

    // LOGIC
    private const int TILE_COUNT_X = 8;
    private const int TILE_COUNT_Y = 8;
    private GameObject[,] tiles;
    private MeshRenderer[,] tileRenderers;
    private Camera currentCamera;
    private Vector2Int currentHover = -Vector2Int.one;

    private void Awake()
    {
        GenerateAllTiles(1, TILE_COUNT_X, TILE_COUNT_Y);
    }
    private void Update() {
        // Handle user input and other updates
        if(!currentCamera)
        {
            currentCamera = Camera.main;
            return;
        }

        if (Mouse.current == null)
            return;

        RaycastHit info;
        Ray ray = currentCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if(Physics.Raycast(ray, out info, 100, LayerMask.GetMask("Tile", "Hover")))
        {
            //Get the indexes of the tile i've hit
            Vector2Int hitPosition = LookupTileIndex(info.transform.gameObject);

            // If we're hovering a tile after not hovering any tiles
            if (currentHover == -Vector2Int.one)
            {
                currentHover = hitPosition;
                SetTileHover(hitPosition, true);
            }

            // If we were already hovering a tile, change the previous one
            if (currentHover != hitPosition)
            {
                SetTileHover(currentHover, false);
                currentHover = hitPosition;
                SetTileHover(hitPosition, true);
            }
        }
        else
        {
            if(currentHover != -Vector2Int.one)
            {
                SetTileHover(currentHover, false);
                currentHover = -Vector2Int.one;
            }
        }
    }

    // Generate the board
    private void GenerateAllTiles(float tileSize, int tileCountX, int tileCountY)
    {
        tiles = new GameObject[tileCountX, tileCountY];
        tileRenderers = new MeshRenderer[tileCountX, tileCountY];
        for (int x = 0; x < tileCountX; x++)
            for (int y = 0; y < tileCountY; y++)
            {
                tiles[x, y] = GenerateSingleTile(tileSize, x, y);
                tileRenderers[x, y] = tiles[x, y].GetComponent<MeshRenderer>();
            }
    }

    private GameObject GenerateSingleTile(float tileSize, int x, int y)
    {
        GameObject tileObject = new GameObject(string.Format("X:{0}, Y:{1}", x, y));
        tileObject.transform.parent = transform;

        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().sharedMaterial = tileMaterial;

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, 0, y * tileSize);
        vertices[1] = new Vector3(x * tileSize, 0, (y + 1) * tileSize);
        vertices[2] = new Vector3((x + 1) * tileSize, 0, y * tileSize);
        vertices[3] = new Vector3((x + 1) * tileSize, 0, (y + 1) * tileSize);

        int[] tris = new int[] { 0, 1, 2, 1, 3, 2 };

        mesh.vertices = vertices;
        mesh.triangles = tris;

        mesh.RecalculateNormals();

        tileObject.layer = LayerMask.NameToLayer("Tile");
        tileObject.AddComponent<BoxCollider>();

        return tileObject;
    }

    private void SetTileHover(Vector2Int position, bool isHovering)
    {
        if (position == -Vector2Int.one)
            return;

        tiles[position.x, position.y].layer = LayerMask.NameToLayer(isHovering ? "Hover" : "Tile");
        tileRenderers[position.x, position.y].sharedMaterial = isHovering && hoverMaterial ? hoverMaterial : tileMaterial;
    }

    //Operations
    private Vector2Int LookupTileIndex(GameObject hitInfo)
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (tiles[x, y] == hitInfo)
                    return new Vector2Int(x, y);

        return -Vector2Int.one; // Invalid
    }
}
