using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UI;

public class RenderUI : MonoBehaviour
{
    public Canvas m_Canvas;
    public Text debugTextPrefab;
    public Text tooltipTextPrefab;
    private Text debugText;
    private Text tooltipText;
    private EntityQuery terrainQuery;
    private EntityQuery terrainFacesQuery;
    private EntityQuery playerQuery;
    private void OnEnable()
    {
        debugText = Instantiate(debugTextPrefab, m_Canvas.transform, false);
        tooltipText = Instantiate(tooltipTextPrefab, m_Canvas.transform, false);
        tooltipText.text = "Press TAB to spawn your player!";
        foreach (var world in World.All)
        {
            if (world.Name == "ClientWorld")
            {
                terrainQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainArea>());
                terrainFacesQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainFace>());
                playerQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerInput>(), ComponentType.ReadOnly<GhostOwnerIsLocal>());
            }
        }
    }

    private void Update()
    {
        foreach (var world in World.All)
        {
            if (world.Name == "ClientWorld")
            {
                int areas = terrainQuery.CalculateEntityCount();
                int faces = terrainFacesQuery.CalculateEntityCount();
                debugText.text = $"NumAreas {areas}\nNumFaces {faces}";
                tooltipText.enabled = playerQuery.CalculateEntityCount() == 0;
                break;
            }
        }
    }
    
}
