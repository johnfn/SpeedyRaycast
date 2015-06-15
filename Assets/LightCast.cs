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

    public Vector2 FurthestPoint
    {
        get { return HasSecondPoint ? SecondPoint : FirstPoint; }
    }

    public void DebugDraw(Vector2 start)
    {
        Debug.DrawLine(start, FurthestPoint, Color.yellow);
        Debug.DrawLine(start, FirstPoint, Color.black);
    }

    public void DebugDrawColor(Vector2 start, Color color)
    {
        Debug.DrawLine(start, FurthestPoint, color);
    }

    public float Angle(Vector2 from)
    {
        return Mathf.Atan2(FurthestPoint.y - from.y, FurthestPoint.x - from.x);
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

        var rays = new List<LightRay>();

        foreach (var vertex in vertices)
        {
            var rayDirection = (vertex - lightOrigin).normalized; // It is important to normalize the direction - otherwise, Unity's raycasting will go into lines if it is too large.
            var hit = Physics2D.Raycast(transform.position, rayDirection);
            var castedLine = new Line {Start = transform.position, End = hit.point};

            if (hit.collider != null)
            {
                var result = new LightRay();

                if (hit.point != vertex)
                {
                    // Unity's raycasting made us go straight through the vertex and had us collide with the wall after the vertex.

                    if (Vector2.Distance(lightOrigin, hit.point) > Vector2.Distance(lightOrigin, vertex))
                    {
                        result.FirstPoint = vertex;
                        result.SecondPoint = hit.point;
                    }
                    else
                    {
                        var success = false;

                        // TODO: COPY AND PASTED CODE LOL

                        var adjacentLines = lines.FindAll(l => l.Start == vertex || l.End == vertex);

                        foreach (var line in adjacentLines)
                        {
                            if (line.LineContainsPoint(castedLine.Start) && line.LineContainsPoint(castedLine.End))
                            {
                                // Continue the raycast from the other side

                                var otherSide = line.Start == vertex ? line.End : line.Start;
                                var nextHit = Physics2D.Raycast(otherSide + rayDirection * 0.01f, rayDirection);

                                result.SecondPoint = nextHit.point;

                                success = true;
                            }
                        }

                        // We collided before we hit the vertex, so we don't need to consider this one.
                        if (!success)
                        {
                            continue;
                        }
                    }
                }
                else
                {
                    result.FirstPoint = hit.point;

                    // Check to see if we're at a corner that we can continue on past.

                    var nextHit = Physics2D.Raycast(vertex + rayDirection * .01f, rayDirection);

                    if (Vector2.Distance(nextHit.point, hit.point) > .02f)
                    {
                        result.SecondPoint = nextHit.point;
                    }
                    else
                    {
                        // Check if we're right on top of a wall. In that case, simply continue the raycast on the other side of the wall.

                        // Find the 2 vertices adjacent to the current vertex. We do this by looking through our line collection. One of these lines
                        // will start with this vertex, and one will end with it.

                        var adjacentLines = lines.FindAll(l => l.Start == vertex || l.End == vertex);

                        foreach (var line in adjacentLines)
                        {
                            if (line.LineContainsPoint(castedLine.Start) && line.LineContainsPoint(castedLine.End))
                            {
                                // Continue the raycast from the other side

                                var otherSide = line.Start == vertex ? line.End : line.Start;

                                nextHit = Physics2D.Raycast(otherSide + rayDirection * 0.01f, rayDirection);

                                result.SecondPoint = nextHit.point;
                            }
                        }

                    }
                }

                // Draw debugging stuff. 

                /*
                if (result.HasSecondPoint)
                {
                    Debug.DrawLine(transform.position, result.SecondPoint, Color.red);
                }

                Debug.DrawLine(transform.position, result.FirstPoint, Color.black);
                */

                rays.Add(result);
            }
            else
            {
                Debug.Log("No collider?");
            }
        }

        var raysGroupedByAngle = new List<List<LightRay>> {new List<LightRay> {rays[0]}};

        for (var i = 1; i < rays.Count; i++)
        {
            var currentRayGroup = raysGroupedByAngle.Last();
            var ray = rays[i];
            var rayAngle = ray.Angle(lightOrigin);

            if (Mathf.Approximately(currentRayGroup[0].Angle(lightOrigin), rayAngle))
            {
                currentRayGroup.Add(ray);
            }
            else
            {
                raysGroupedByAngle.Add(new List<LightRay> {ray});
            }
        }

        polygons.Clear();

        for (var i = 0; i < raysGroupedByAngle.Count(); i++)
        {
            if (raysGroupedByAngle[i].Count != 1)
            {
                Debug.Log(raysGroupedByAngle[i].Count);
            }

            var group = raysGroupedByAngle[i]
                .OrderBy(x => Vector2.Distance(lightOrigin, x.FurthestPoint));

            var nextGroup = raysGroupedByAngle[(i + 1) % raysGroupedByAngle.Count()]
                .OrderBy(x => Vector2.Distance(lightOrigin, x.FurthestPoint));

            foreach (var r1 in group)
            {
                foreach (var r2 in nextGroup)
                {
                    var bound = FindTriangleBound(r1, r2, lines);

                    if (bound == default(Line)) continue;

                    polygons.Add(new List<Vector2>
                    {
                        lightOrigin,
                        bound.Start,
                        bound.End
                    });

                    goto Done;
                }
            }

            foreach (var r1 in group) r1.DebugDraw(lightOrigin);
            foreach (var r1 in nextGroup) r1.DebugDraw(lightOrigin);

            Done:
            ;

        }

        /*
        for (var i = 0; i < rays.Count; i++)
        {
            var ray = rays[i];
            var nextRay = rays[(i + 1) % rays.Count];
            var bound = FindTriangleBound(ray, nextRay, lines);

            if (bound == default(Line))
            {
                Debug.Log("Bad");
                Debug.Log(i);
                Debug.Log(transform.position.x);
                Debug.Log(transform.position.y);

                ray.DebugDraw(transform.position);
                nextRay.DebugDraw(transform.position);
            }

            polygons.Add(new List<Vector2>
            {
                lightOrigin,
                bound.Start,
                bound.End
            });
        }
        */
    }

    /*
    bool DoRaysOvelap(LightRay r1, LightRay r2)
    {
        var l1 = new Line {Start = lightOrigin, End = r1.FurthestPoint};
        var l2 = new Line {Start = lightOrigin, End = r2.FurthestPoint};

        var shortestLine = l1.Length > l2.Length ? l2 : l1;
        var longestLine = l1.Length > l2.Length ? l1 : l2;

        if (longestLine.SegmentContainsPoint(shortestLine.End))
        {
            overlappedLines.Add(shortestLine.End == r1.FurthestPoint ? r1 : r2);
        }
    }
    */

    Line FindTriangleBound(LightRay ray, LightRay nextRay, List<Line> lines)
    {
        var potentialBounds = new List<Line>
        {
            new Line {Start = ray.FirstPoint,    End = nextRay.FirstPoint},
            new Line {Start = ray.FurthestPoint, End = nextRay.FirstPoint},
            new Line {Start = ray.FirstPoint,    End = nextRay.FurthestPoint},
            new Line {Start = ray.FurthestPoint, End = nextRay.FurthestPoint}
        };

        foreach (var potentialBound in potentialBounds)
        {
            if (lines.Exists(l => l.SegmentContainsPoint(potentialBound.Start) && l.SegmentContainsPoint(potentialBound.End)))
            {
                return potentialBound;
            }
        }

        return default(Line);
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

        var c1 = 0;
        var c2 = vertex.Length - 1;
        var c3 = vertex.Length - 2;

        for (x = 0; x < tris.Length; x += 3)
        {
            tris[x] = c1;
            tris[x + 1] = c2;
            tris[x + 2] = c3;

            c2--;
            c3--;
        }

        //Assign data to mesh
        mesh.vertices = vertex;
        mesh.uv = uvs;
        mesh.triangles = tris;

        //Recalculations
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.Optimize();

        //Return the mesh
        return mesh;
    }

}
