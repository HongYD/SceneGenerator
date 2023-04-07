using System;
using System.Collections.Generic;
using UnityEngine;

public enum HeightPattern
{
    Flat,
    Saddle,
    Ramp,
    One_Hight,
    Three_Hight,
    Steep,
    Count,
}

//Mesh的数据结构
public class MeshStructure : SingletonMono<MeshStructure>
{
    private SquareGrid squareGrid;
    private List<Vector3> vertices;//点位置List
    private List<int> triangles;//三角形顶点索引
    private List<Triangle> trianglesMesh;
    private List<List<int>> outlines = new List<List<int>>();
    HashSet<int> checkedVertices= new HashSet<int>();
    private Vector2[] uvs;

    public int Size
    {
        get { return trianglesMesh.Count; }
    }

    //所有点的邻接三角形的字典
    Dictionary<int,List<Triangle>>trianglesDict = new Dictionary<int,List<Triangle>>();

    protected override void Awake()
    {
        base.Awake();
    }

    public void GenerateMesh(Grid grid,float squareSize,MeshFilter floorMesh,MeshFilter wallMesh, float[,] heightMap, string randomSeed)
    {
        trianglesDict.Clear();
        outlines.Clear();
        vertices = new List<Vector3>();
        triangles = new List<int>();
        trianglesMesh = new List<Triangle>();
        checkedVertices.Clear();

        squareGrid = new SquareGrid(grid, squareSize, heightMap);
        for (int i = 0; i < squareGrid.squares.GetLength(0); i++)
        {
            for(int j = 0; j < squareGrid.squares.GetLength(1); j++)
            {
                TriangulateSquare(squareGrid.squares[i, j]);
            }
        }

        Mesh mesh = new Mesh();
        floorMesh.mesh = mesh;
        mesh.vertices= vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        GenerateWallsMesh(wallMesh);
    }

    #region Generate walls
    private void GenerateWallsMesh(MeshFilter walls)
    {
        MeshCollider currentCollider = GetComponent<MeshCollider>();
        Destroy(currentCollider);
        CalculateMeshOutlines();

        List<Vector3> wallVertices = new List<Vector3>();
        List<int> wallTriangles = new List<int>();
        Mesh wallMesh = new Mesh();
        int wallHeight = 5;

        foreach (List<int> outline in outlines)
        {
            for (int i = 0; i < outline.Count - 1; i++)
            {
                int startIndex = wallVertices.Count;
                wallVertices.Add(vertices[outline[i]]); // left
                wallVertices.Add(vertices[outline[i + 1]]); // right
                wallVertices.Add(vertices[outline[i]] - Vector3.up * wallHeight); // bottom left
                wallVertices.Add(vertices[outline[i + 1]] - Vector3.up * wallHeight); // bottom right

                //逆时针渲染，法向量朝外
                wallTriangles.Add(startIndex + 0);
                wallTriangles.Add(startIndex + 3);
                wallTriangles.Add(startIndex + 2);

                wallTriangles.Add(startIndex + 3);
                wallTriangles.Add(startIndex + 0);
                wallTriangles.Add(startIndex + 1);
            }
        }
        wallMesh.vertices = wallVertices.ToArray();
        wallMesh.triangles = wallTriangles.ToArray();
        wallMesh.RecalculateNormals();
        walls.mesh = wallMesh;
    }

    private void CalculateMeshOutlines()
    {
        for(int vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++)
        {
            if (!checkedVertices.Contains(vertexIndex))
            {
                int newOutlineVertex = GetConnectedOutlineVertex(vertexIndex);
                if(newOutlineVertex!= -1)
                {
                    checkedVertices.Add(vertexIndex);
                    List<int> newOutline = new List<int>();
                    newOutline.Add(vertexIndex);
                    outlines.Add(newOutline);
                    FollowOutline(newOutlineVertex, outlines.Count - 1);
                    outlines[outlines.Count - 1].Add(vertexIndex);
                }
            }
        }
        SimplifyMeshOutlines();
    }

    private void SimplifyMeshOutlines()
    {
        for (int outlineIndex = 0; outlineIndex < outlines.Count; outlineIndex++)
        {
            List<int> simplifiedOutline = new List<int>();
            Vector3 dirOld = Vector3.zero;
            for (int i = 0; i < outlines[outlineIndex].Count; i++)
            {
                Vector3 p1 = vertices[outlines[outlineIndex][i]];
                Vector3 p2 = vertices[outlines[outlineIndex][(i + 1) % outlines[outlineIndex].Count]];
                Vector3 dir = p2 - p1;
                if (dir != dirOld)
                {
                    dirOld = dir;
                    simplifiedOutline.Add(outlines[outlineIndex][i]);
                }
            }
            outlines[outlineIndex] = simplifiedOutline;
        }
    }

    private void FollowOutline(int vertexIndex, int outlineIndex)
    {
        outlines[outlineIndex].Add(vertexIndex);
        checkedVertices.Add(vertexIndex);
        int nextVertexIndex = GetConnectedOutlineVertex(vertexIndex);
        if(nextVertexIndex!= -1)
        {
            FollowOutline(nextVertexIndex, outlineIndex);
        }
    }

    private int GetConnectedOutlineVertex(int vertexIndex)
    {
        List<Triangle> trianglesContainingVertex = trianglesDict[vertexIndex];
        for(int i=0;i<trianglesContainingVertex.Count;i++)
        {
            Triangle triangle = trianglesContainingVertex[i];
            for(int j = 0; j < 3; j++)
            {
                int vertexB = triangle[j];
                if(vertexB != vertexIndex && !checkedVertices.Contains(vertexB))
                {
                    if (IsOutlineEdge(vertexIndex, vertexB))
                    {
                        return vertexB;
                    }
                }
            }
        }
        return -1;
    }

    //如果两个相邻的点的共同邻接三角形只有一个，那么这两个点组成的线就是整个地形的外边
    //两点的“相邻”定义为两点在同一个三角形中
    private bool IsOutlineEdge(int vertexA, int vertexB)
    {
        List<Triangle> triangleContainsVertexA  = trianglesDict[vertexA];
        int sharedTriangleCount = 0;
        for(int i =0;i<triangleContainsVertexA.Count;i++)
        {
            if (triangleContainsVertexA[i].Contains(vertexB))
            {
                sharedTriangleCount++;
                if (sharedTriangleCount > 1)
                {
                    break;
                }
            }
        }
        return sharedTriangleCount == 1;
    }
    #endregion

    //Matching Square
    void TriangulateSquare(Square square)
    {
        switch (square.configuration)
        {
            case 0:
                break;
            case 1:
                MeshFromPoints(square.midLeft, square.midBottom, square.bottomLeft);
                break;
            case 2:
                MeshFromPoints(square.bottomRight, square.midBottom, square.midRight);
                break;
            case 3:
                MeshFromPoints(square.midRight, square.bottomRight, square.bottomLeft, square.midLeft);
                break;
            case 4:
                MeshFromPoints(square.topRight, square.midRight, square.midTop);
                break;
            case 5:
                MeshFromPoints(square.midTop, square.topRight, square.midRight, square.midBottom, square.bottomLeft, square.midLeft);
                break;
            case 6:
                MeshFromPoints(square.midTop, square.topRight, square.bottomRight, square.midBottom);
                break;
            case 7:
                MeshFromPoints(square.midTop, square.topRight, square.bottomRight, square.bottomLeft, square.midLeft);
                break;
            case 8:
                MeshFromPoints(square.topLeft, square.midTop, square.midLeft);
                break;
            case 9:
                MeshFromPoints(square.topLeft, square.midTop, square.midBottom, square.bottomLeft);
                break;
            case 10:
                MeshFromPoints(square.topLeft, square.midTop, square.midRight, square.bottomRight, square.midBottom, square.midLeft);
                break;
            case 11:
                MeshFromPoints(square.topLeft, square.midTop, square.midRight, square.bottomRight, square.bottomLeft);
                break;
            case 12:
                MeshFromPoints(square.topLeft, square.topRight, square.midRight, square.midLeft);
                break;
            case 13:
                MeshFromPoints(square.topLeft, square.topRight, square.midRight, square.midBottom, square.bottomLeft);
                break;
            case 14:
                MeshFromPoints(square.topLeft, square.topRight, square.bottomRight, square.midBottom, square.midLeft);
                break;
            case 15:
                MeshFromPoints(square.topLeft, square.topRight, square.bottomRight, square.bottomLeft);
                break;
        }
    }

    void MeshFromPoints(params Point[] points)
    {
        AssignVertices(points);
        if (points.Length >= 3)
            CreateTriangle(points[0], points[1], points[2]);
        if (points.Length >= 4)
            CreateTriangle(points[0], points[2], points[3]);
        if (points.Length >= 5)
            CreateTriangle(points[0], points[3], points[4]);
        if (points.Length >= 6)
            CreateTriangle(points[0], points[4], points[5]);
    }

    void AssignVertices(Point[] points)
    {
        for(int i=0;i<points.Length;i++)
        {
            if (points[i].vertexIndex == -1)
            {
                points[i].vertexIndex = vertices.Count;
                vertices.Add(points[i].position);
            }
        }
    }

    void CreateTriangle(Point a, Point b, Point c) 
    {
        triangles.Add(a.vertexIndex);
        triangles.Add(b.vertexIndex);
        triangles.Add(c.vertexIndex);

        Triangle triangle = new Triangle(a, b, c);
        triangle.index = trianglesMesh.Count;
        trianglesMesh.Add(triangle);
        AddTriangleToDictionary(triangle.vertexIndexA, triangle);
        AddTriangleToDictionary(triangle.vertexIndexB, triangle);
        AddTriangleToDictionary(triangle.vertexIndexC, triangle);

    }

    void AddTriangleToDictionary(int vertexIndexKey, Triangle triangle)
    {
        if (trianglesDict.ContainsKey(vertexIndexKey))
        {
            trianglesDict[vertexIndexKey].Add(triangle);
        }
        else
        {
            List<Triangle>trianglesList = new List<Triangle>();
            trianglesList.Add(triangle);
            trianglesDict.Add(vertexIndexKey, trianglesList);
        }
    }
}

//基本点
public class Point
{
    public Vector3 position;
    public int vertexIndex = -1;

    public Point(Vector3 _pos)
    {
        position = _pos;
    }
}

//控制点
public class ControlPoint : Point
{
    public bool active;
    public Point above, right;

    public ControlPoint(Vector3 _pos, bool _active, float squareSize) : base(_pos)
    {
        active = _active;
        above = new Point(position + Vector3.forward * squareSize / 2.0f);
        right = new Point(position + Vector3.right * squareSize / 2.0f);
    }
}

//四边形
public class Square
{
    public float heightStep = 0.2f;
    public ControlPoint topLeft, topRight, bottomLeft, bottomRight;
    public Point midTop, midBottom, midLeft, midRight;
    public int configuration;

    public Square(ControlPoint _topLeft, ControlPoint _topRight, ControlPoint _bottomRight, ControlPoint _bottomLeft)
    {
        topLeft = _topLeft;
        topRight = _topRight;
        bottomRight = _bottomRight;
        bottomLeft = _bottomLeft;

        GetCurrentHeightMap();

        midTop = topLeft.right;
        midRight = bottomRight.above;
        midBottom = bottomLeft.right;
        midLeft = bottomLeft.above;


        if (topLeft.active)
            configuration += 8;
        if (topRight.active)
            configuration += 4;
        if (bottomRight.active)
            configuration += 2;
        if (bottomLeft.active)
            configuration += 1;
    }

    public void GetCurrentHeightMap()
    {
        System.Random random = new System.Random();
        switch(random.Next(0, (int)HeightPattern.Count)+2)
        {
            case 0:
                ModifyVertexHightByPattern(HeightPattern.Flat);
                break;
            case 1:
                ModifyVertexHightByPattern(HeightPattern.Saddle);
                break;
            case 2:
                ModifyVertexHightByPattern(HeightPattern.Ramp);
                break;
            case 3:
                ModifyVertexHightByPattern(HeightPattern.One_Hight);
                break;
            case 4:
                ModifyVertexHightByPattern(HeightPattern.Three_Hight);
                break;
            case 5:
                ModifyVertexHightByPattern(HeightPattern.Steep);
                break;
            default:
                ModifyVertexHightByPattern(HeightPattern.Flat);
                break;
        }
    }

    public void ModifyVertexHightByPattern(HeightPattern pattern)
    {
        switch(pattern)
        {
            case HeightPattern.Flat:
                break;
            case HeightPattern.Saddle:
                bottomLeft.position.y += heightStep;
                topRight.position.y += heightStep;
                break;
            case HeightPattern.Ramp:
                topRight.position.y += heightStep;
                topLeft.position.y += heightStep;
                break;
            case HeightPattern.One_Hight:
                topLeft.position.y += heightStep;
                break;
            case HeightPattern.Three_Hight:
                bottomLeft.position.y += heightStep;
                topRight.position.y += heightStep;
                break;
            case HeightPattern.Steep:
                bottomLeft.position.y += heightStep;
                topLeft.position.y += heightStep;
                break;
            default:
                break;

        }
    }
}

//四边形网格
public class SquareGrid
{
    public Square[,] squares;
    public SquareGrid(Grid grid, float squareSize, float[,] heightMap)
    {
        int nodeCountX = grid.gridXNodeNum;
        int nodeCountY = grid.gridYNodeNum;

        ControlPoint[,] controlPoints = new ControlPoint[nodeCountX, nodeCountY];
        for(int x=0;x<nodeCountX;x++)
        {
            for(int y=0;y<nodeCountY;y++)
            {
                Vector3 pos = new Vector3((grid.grid[x, y].worldPosition.x - grid.nodeRadius), /*heightMap[x, y]*/1.0f, (grid.grid[x, y].worldPosition.z - grid.nodeRadius));
                controlPoints[x, y] = new ControlPoint(pos, true, squareSize);
            }
        }

        squares = new Square[nodeCountX - 1, nodeCountY - 1];
        for(int x = 0;x<nodeCountX - 1;x++)
        {
            for(int y =0;y<nodeCountY - 1;y++)
            {
                squares[x, y] = new Square(controlPoints[x, y + 1], controlPoints[x + 1, y + 1], controlPoints[x + 1, y], controlPoints[x, y]);
            }
        }
    }
}

//三角形
public class Triangle
{
    //顺时针
    public int vertexIndexA;
    public int vertexIndexB;
    public int vertexIndexC;
    int[] vertices;
    public Vector3[] VPos;
    public Vector3 pos;
    public int index = -1;

    public int HeapIndex { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public Triangle(Point a,Point b, Point c)
    {
        vertexIndexA = a.vertexIndex;
        vertexIndexB = b.vertexIndex;
        vertexIndexC = c.vertexIndex;

        vertices = new int[3];
        vertices[0] = a.vertexIndex;
        vertices[1] = b.vertexIndex;
        vertices[2] = c.vertexIndex;

        VPos = new Vector3[3];
        VPos[0] = a.position;
        VPos[1] = b.position;
        VPos[2] = c.position;


        //三角形的position是三角形内心位置
        float ab = Mathf.Sqrt((VPos[0].x - VPos[1].x) * (VPos[0].x - VPos[1].x) + 0 + (VPos[0].z - VPos[1].z) * (VPos[0].z - VPos[1].z));
        float bc = Mathf.Sqrt((VPos[1].x - VPos[2].x) * (VPos[1].x - VPos[2].x) + 0 + (VPos[1].z - VPos[2].z) * (VPos[1].z - VPos[2].z));
        float ca = Mathf.Sqrt((VPos[2].x - VPos[0].x) * (VPos[2].x - VPos[0].x) + 0 + (VPos[2].z - VPos[0].z) * (VPos[2].z - VPos[0].z));
        pos.x = (ab * VPos[0].x + bc * VPos[1].x + ca * VPos[2].x) / (ab + bc + ca);
        pos.y = 1.0f;
        pos.z = (ab * VPos[0].z + bc * VPos[1].z + ca * VPos[2].z) / (ab + bc + ca);
    }

    public int this[int i]
    {
        get
        {
            return vertices[i];
        }
    }

    public bool Contains(int vertexIndex)
    {
        return vertexIndex == vertexIndexA || vertexIndex == vertexIndexB || vertexIndex == vertexIndexC;
    }
}


