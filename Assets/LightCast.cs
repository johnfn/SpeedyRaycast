using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

struct LightRay
{
    public Vector2 FirstPoint;

    private Vector2 _secondPoint;
    public Vector2 SecondPoint
    {
        get { return _secondPoint; }
        set
        {
            HasSecondPoint = true;
            _secondPoint = value;
        }
    }

    public bool HasSecondPoint { get; private set; }
}

public class LightCast : MonoBehaviour
{
    public List<GameObject> gameobjectPool; // TODO rename

    public Material GenericMaterial;

    public List<List<Vector2>> polygons;

    public PolygonCollider2D Collider;

	// Use this for initialization
	void Start ()
	{
	    polygons = new List<List<Vector2>>();

	    for (var i = 0; i < 100; i++)
	    {
	        gameobjectPool.Add(new GameObject());
	    }

	    foreach (var obj in gameobjectPool)
	    {
	        obj.AddComponent<MeshFilter>();
	        obj.AddComponent<MeshRenderer>();
	    }
	}
	
	// Update is called once per frame
	void Update () {
	    CastLight();

	    for (var i = 0; i < polygons.Count; i++)
	    {
	        var mesh = gameobjectPool[i].GetComponent<Mesh>();
	        if (mesh)
	        {
	            gameobjectPool[i].GetComponent<Mesh>().Clear();
	        }

	        DrawPolygon(polygons[i], gameobjectPool[i]);
	    }
	}

    void DrawPoint(float x, float y, Color c)
    {
        const float size = 0.2f;

        Debug.DrawLine(new Vector2(x - size, y), new Vector2(x + size, y), c);
        Debug.DrawLine(new Vector2(x, y - size), new Vector2(x, y + size), c);
    }

    void CastLight()
    {
        var myPosition = new Vector2(transform.position.x, transform.position.y);

        var points = new List<Vector2>(); 

        for (var j = 0; j < Collider.pathCount; j++)
        {
            foreach (var point in Collider.GetPath(j))
            {
                if (!points.Contains(point))
                {
                    points.Add(point);
                }
            }
        }

        // Sort points radially.

        points.Sort((point1, point2) =>
	    {
            var angle1 = Mathf.Atan2(point1.y - transform.position.y, point1.x - transform.position.x);
            var angle2 = Mathf.Atan2(point2.y - transform.position.y, point2.x - transform.position.x);

	        return angle1.CompareTo(angle2);
	    });

        var lightPositions = new List<LightRay>();

        foreach (var vertex in points)
        {
            var dbgColor = Color.black;
            var rayDirection = (vertex - myPosition).normalized;

            var hit = Physics2D.Raycast(transform.position, rayDirection);

            if (hit.collider != null)
            {
                var result = new LightRay();

                if (hit.point != vertex)
                {
                    // Unity's raycasting made us go straight through the vertex and had us collide with the wall after the vertex.

                    if (Vector2.Distance(myPosition, hit.point) > Vector2.Distance(myPosition, vertex))
                    {
                        result.FirstPoint = vertex;
                        result.SecondPoint = hit.point;
                    }
                    else
                    {
                        // We collided before we hit the vertex, so we don't need to consider this one.
                        continue;
                    }
                }
                else
                {
                    result.FirstPoint = hit.point;

                    // Check to see if we're at a corner that we can continue on past.

                    var nextHit = Physics2D.Raycast(vertex + rayDirection * .01f, rayDirection);

                    if (Vector2.Distance(nextHit.point, hit.point) > .01f)
                    {
                        result.SecondPoint = nextHit.point;
                    }
                }

                // Draw debugging stuff. 

                if (result.HasSecondPoint)
                {
                    Debug.DrawLine(transform.position, result.SecondPoint, Color.red);
                }

                Debug.DrawLine(transform.position, result.FirstPoint, Color.black);

                lightPositions.Add(result);
            }
            else
            {
                Debug.Log("No collider??");
            }
        }

        /*
        polygons.Clear();

        for (var i = 0; i < lightPositions.Count; i++)
        {
            var nextI = (i + 1) % lightPositions.Count;

            Debug.Log(lightPositions[i].HasSecondPoint);

            if (!lightPositions[i].HasSecondPoint && !lightPositions[nextI].HasSecondPoint)
            {
                polygons.Add(new List<Vector2>
                {
                    transform.position,
                    lightPositions[i].FirstPoint,
                    lightPositions[nextI].FirstPoint
                });

                Debug.Log("Hello.");
            }
        }

        */
    }

    void DrawPolygon(List<Vector2> nodePositions, GameObject obj)
    {
        //Components
        var mf = obj.GetComponent<MeshFilter>();
        mf.mesh.Clear();

        var mr = obj.GetComponent<MeshRenderer>();

        //Create mesh
        var mesh = CreateMesh(nodePositions);

        //Assign materials
        mr.material = GenericMaterial;

        //Assign mesh to game object
        mf.mesh = mesh;
    }

    Mesh CreateMesh(List<Vector2> nodes)
    {
        int x;

        //Create a new mesh
        var mesh = new Mesh();

        //Vertices
        var vertex = new Vector3[nodes.Count];

        for (x = 0; x < nodes.Count; x++)
        {
            vertex[x] = nodes[x];
        }

        //UVs
        var uvs = new Vector2[vertex.Length];

        for (x = 0; x < vertex.Length; x++)
        {
            if ((x % 2) == 0)
            {
                uvs[x] = new Vector2(0, 0);
            }
            else
            {
                uvs[x] = new Vector2(1, 1);
            }
        }

        //Triangles
        var tris = new int[3 * (vertex.Length - 2)];

        var C1 = 0;
        var C2 = vertex.Length - 1;
        var C3 = vertex.Length - 2;

        for (x = 0; x < tris.Length; x += 3)
        {
            tris[x] = C1;
            tris[x + 1] = C2;
            tris[x + 2] = C3;

            C2--;
            C3--;
        }

        //Assign data to mesh
        mesh.vertices = vertex;
        mesh.uv = uvs;
        mesh.triangles = tris;

        //Recalculations
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.Optimize();

        //Name the mesh
        mesh.name = "MyMesh";

        //Return the mesh
        return mesh;
    }

}
