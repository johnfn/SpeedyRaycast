using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

class MyUtil
{
    public static bool CloseEnough4Me(float x, float y)
    {
        return Mathf.Abs(x - y) < 0.001;
    }

    public static bool IsWithinRange(float value, float start, float end)
    {
        // (The .Approximately() is likely unnecessary in this case, but it silences some Resharper errors.)

        return CloseEnough4Me(Mathf.Clamp(value, Mathf.Min(start, end), Mathf.Max(start, end)), value);
    }
}

struct Line
{
    public Vector2 Start;

    public Vector2 End;

    public bool SegmentContainsPoint(Vector2 point)
    {
        return LineContainsPoint(point) && 
            MyUtil.IsWithinRange(point.x, Start.x, End.x) && // The point is within the segment.
            MyUtil.IsWithinRange(point.y, Start.y, End.y);
    }

    public bool LineContainsPoint(Vector2 point)
    {
        // Special case for vertical lines (which have an undefined slope).
        if (Mathf.Abs(End.x - Start.x) < .001f)
        {
            return MyUtil.CloseEnough4Me(point.x, Start.x);
        }

        var m = (End.y - Start.y) / (End.x - Start.x);
        var b = Start.y - m * Start.x;

        return MyUtil.CloseEnough4Me(point.y, m * point.x + b);
    }

    public float Length
    {
        get { return Vector2.Distance(Start, End);  }
    }

    public static bool operator ==(Line x, Line y)
    {
        return x.Start == y.Start && x.End == y.End;
    }

    public static bool operator !=(Line x, Line y)
    {
        return x.Start != y.Start || x.End != y.End;
    }

    public void DebugDraw()
    {
        Debug.DrawLine(Start, End, Color.red);
    }
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

	    foreach (var go in gameobjectPool)
	    {
            go.SetActive(false);
	    }

	    for (var i = 0; i < polygons.Count; i++)
	    {
	        gameobjectPool[i].SetActive(true);

	        DrawTriangle(polygons[i], gameobjectPool[i]);
	    }
	}

    void DrawPoint(float x, float y, Color c)
    {
        const float size = 0.2f;

        Debug.DrawLine(new Vector2(x - size, y), new Vector2(x + size, y), c);
        Debug.DrawLine(new Vector2(x, y - size), new Vector2(x, y + size), c);
    }

    private void CastLight()
    {
        var lightOrigin = new Vector2(transform.position.x, transform.position.y);

        var vertices = new List<Vector2>();
        var lines = new List<Line>();

        for (var i = 0; i < Collider.pathCount; i++)
        {
            var path = Collider.GetPath(i);

            for (var j = 0; j < path.Length; j++)
            {
                var point = path[j];
                var nextPoint = path[(j + 1) % path.Length];

                // Gather all points

                if (!vertices.Contains(point))
                {
                    vertices.Add(point);
                }

                // Gather all lines

                lines.Add(new Line
                {
                    Start = point,
                    End = nextPoint
                });
            }
        }

        // Sort points radially, counterclockwise.

        vertices.Sort((point1, point2) =>
        {
            var angle1 = Mathf.Atan2(point1.y - transform.position.y, point1.x - transform.position.x);
            var angle2 = Mathf.Atan2(point2.y - transform.position.y, point2.x - transform.position.x);

            return angle1.CompareTo(angle2);
        });

        // Bucket vertices by angle.

        var groupedVertices =
            vertices
                .GroupBy(vert => Mathf.Atan2(vert.y - transform.position.y, vert.x - transform.position.x))
                .Select(list => list.OrderBy(vert => Vector2.Distance(vert, lightOrigin)).ToList())
                .ToList();

        var groupedVisibleVerticies = new List<List<Vector2>>();

        // Transform vertex group list into *visible* vertex group list.

        foreach (var vertexGroup in groupedVertices)
        {
            var visibleVerticies = new List<Vector2>();

            var raycastingPath = new List<Vector2> {lightOrigin};
            raycastingPath.AddRange(vertexGroup);

            var rayDirection = (raycastingPath[1] - raycastingPath[0]).normalized;

            for (var j = 0; j < raycastingPath.Count() - 1; j++)
            {
                var start = raycastingPath[j];
                var targetVertex = raycastingPath[j + 1];

                var hit = Physics2D.Raycast(start + rayDirection * 0.01f, rayDirection);

                if (hit.collider == null)
                {
                    return; //TODO heh
                }

                var madeItToTheNextVertex = MyUtil.CloseEnough4Me(hit.point.x, targetVertex.x) && MyUtil.CloseEnough4Me(hit.point.y, targetVertex.y);

                // If we are running directly on top of any edge, don't allow it to stop us.
                if (lines.Exists(l => l.Start == start && l.End == targetVertex || l.End == start && l.Start == targetVertex))
                {
                    madeItToTheNextVertex = true;
                }

                // On a corner, Unity's raycasting may have us go straight through the vertex and collide with something after the vertex.
                if (Vector2.Distance(lightOrigin, hit.point) > Vector2.Distance(lightOrigin, targetVertex))
                {
                    madeItToTheNextVertex = true;
                }

                if (!madeItToTheNextVertex) break;

                visibleVerticies.Add(targetVertex);
            }

            if (visibleVerticies.Count == 0) continue;

            groupedVisibleVerticies.Add(visibleVerticies);

            // Check if we're on a vertex which is a corner, and attempt to go past it.

            var finalHit = Physics2D.Raycast(visibleVerticies.Last() + rayDirection * .01f, rayDirection);

            if (Vector2.Distance(finalHit.point, visibleVerticies.Last()) > .02f)
            {
                visibleVerticies.Add(finalHit.point);
            }
        }

        polygons.Clear();

        /*
        for (var i = 0; i < groupedVisibleVerticies.Count; i++)
        {
            var group = groupedVisibleVerticies[i];
            var nextGroup = groupedVisibleVerticies[(i + 1) % groupedVisibleVerticies.Count];

            var bound = FindTriangleBound(group, nextGroup, lines);

            if (bound == default(Line))
            {
                Debug.Log("Couldn't bound triangle!");

                Debug.DrawLine(lightOrigin, group.Last(), Color.red);
                Debug.DrawLine(lightOrigin, nextGroup.Last(), Color.red);
            }

            polygons.Add(new List<Vector2>
            {
                lightOrigin,
                bound.Start,
                bound.End
            });
        }
        */

        polygons.Add(new List<Vector2>
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1)

        });
    }

    Line FindTriangleBound(List<Vector2> group, List<Vector2> nextGroup, List<Line> lines)
    {
        foreach (var v1 in group)
        {
            foreach (var v2 in nextGroup)
            {
                if (lines.Exists(l => l.SegmentContainsPoint(v1) && l.SegmentContainsPoint(v2)))
                {
                    return new Line {Start = v1, End = v2};
                }
            }
        }

        return default(Line);
    }
    
    /*
        DrawTriangle - takes a list of coordinates for a triangle, and a GameObject obj that will 
        render it, and creates the mesh on that GameObject.
    */
    void DrawTriangle(List<Vector2> nodePositions, GameObject obj)
    {
        var meshFilter = obj.GetComponent<MeshFilter>();
        var meshRenderer = obj.GetComponent<MeshRenderer>();

        meshRenderer.material = GenericMaterial;
        meshFilter.mesh = CreateMesh(nodePositions);
    }

    Mesh CreateMesh(List<Vector2> nodes)
    {
        var mesh = new Mesh
        {
            vertices = new Vector3[] {nodes[0], nodes[1], nodes[2]},
            uv = new[] {new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0)},
            triangles = new[] {0, 2, 1}
        };

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.Optimize();

        return mesh;
    }

}
