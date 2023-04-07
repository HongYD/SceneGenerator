using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class MeshGenerator : MonoBehaviour
{
    [SerializeField]
    private string randomSeed;
    public MeshFilter floor;
    public MeshFilter walls;
    private float[,] heightMap;

    [Range(0, 10.0f)]
    public float scale;
    [Range(0, 5)]
    public int octaves;
    [Range(0, 1.0f)]
    public float persistance;
    [Range(0, 10)]
    public float lacunarity;

    public void LaunchScene()
    {
        Grid.Instance.InitGrid();
        Grid.Instance.CreateGrid();
        randomSeed = Time.time.ToString();
        heightMap = Commons.GetPerlinNoiseHeightMap(Grid.Instance.gridXNodeNum, Grid.Instance.gridYNodeNum, scale, octaves, persistance, lacunarity);
        MeshStructure.Instance.GenerateMesh(Grid.Instance, Grid.Instance.nodeRadius * 2.0f,floor,walls, heightMap, randomSeed);
    }
}
