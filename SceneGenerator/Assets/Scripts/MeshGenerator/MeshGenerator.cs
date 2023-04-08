using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class MeshGenerator : MonoBehaviour
{
    [SerializeField]
    public MeshFilter floor;
    public MeshFilter walls;
    public static string randomSeed;

    public void LaunchScene()
    {
        Grid.Instance.InitGrid();
        Grid.Instance.CreateGrid();
        randomSeed = Time.time.ToString();
        MeshStructure.Instance.GenerateMesh(Grid.Instance, Grid.Instance.nodeRadius * 2.0f,floor,walls);
    }
}
