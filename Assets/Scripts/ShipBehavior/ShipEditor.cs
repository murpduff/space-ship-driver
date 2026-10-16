using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ShipEditor : MonoBehaviour {
    [SerializeField] Tilemap tilemap;
    [SerializeField] Tile[] tiles;
    public bool triggerDisplay = false;
    Dictionary<Vector2Int, ShipPart> shipMap = new Dictionary<Vector2Int, ShipPart>();
    void Start() {
        InitializeStarterShip();
    }

    void OnValidate() {
        if (triggerDisplay) {
            triggerDisplay = false;
            InitializeStarterShip();
        }
    }
    void InitializeStarterShip() {
        shipMap.Clear();
        shipMap.Add(new Vector2Int(0,0), new ShipPart(1, 0));
        shipMap.Add(new Vector2Int(0, -1), new ShipPart(1, 0));
        shipMap.Add(new Vector2Int(1, 0), new ShipPart(1, 0));

        BuildShip();
    }

    void BuildShip() {
        Vector2Int[] neighbors = new Vector2Int[4]{new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1)};

        foreach(Vector2Int key in shipMap.Keys) {
            tilemap.SetTile(new Vector3Int(key.x, key.y, 0), tiles[shipMap[key].id]);

            for(int i = 0; i < 4; i++) {
                Vector2Int pos = key + neighbors[i];
                if(!shipMap.ContainsKey(pos)) {
                    tilemap.SetTile(new Vector3Int(pos.x, pos.y, 0), tiles[0]);
                }
            }
        }
    }

    struct ShipPart {
        public int id;
        public int rotation;
        public ShipPart(int id, int rotation) {
            this.id = id;
            this.rotation = rotation;
        }
    }
}
