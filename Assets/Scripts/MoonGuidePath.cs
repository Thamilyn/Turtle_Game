using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TurtleGame
{
    /// <summary>Plans lateral detours with room for the turtle and the moon's sway.</summary>
    internal sealed class MoonGuidePath
    {
        private readonly Terrain terrain;
        private readonly Transform player;
        private readonly float clearance;
        private readonly List<Rect> obstacles = new List<Rect>();
        private readonly List<Vector2> nodes = new List<Vector2>();
        private readonly Rect area;

        public MoonGuidePath(Terrain terrain, Transform player, float clearance)
        {
            this.terrain = terrain;
            this.player = player;
            this.clearance = clearance;
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            area = new Rect(origin.x + clearance, origin.z + clearance,
                size.x - clearance * 2f, size.z - clearance * 2f);
        }

        public void Refresh(Scene scene)
        {
            obstacles.Clear();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Collider collider in root.GetComponentsInChildren<Collider>())
                {
                    if (!collider.enabled || collider.isTrigger || collider is TerrainCollider ||
                        collider.transform.IsChildOf(player))
                        continue;

                    Bounds bounds = collider.bounds;
                    float ground = terrain.SampleHeight(bounds.center) + terrain.transform.position.y;
                    // Flat road surfaces are walkable; even low logs must be walked around.
                    if (bounds.max.y <= ground + 0.15f)
                        continue;
                    obstacles.Add(Rect.MinMaxRect(bounds.min.x - clearance, bounds.min.z - clearance,
                        bounds.max.x + clearance, bounds.max.z + clearance));
                }
            }
        }

        public bool IsPointClear(Vector2 point)
        {
            if (!area.Contains(point))
                return false;
            foreach (Rect obstacle in obstacles)
                if (obstacle.Contains(point))
                    return false;
            return true;
        }

        public bool IsSegmentClear(Vector2 from, Vector2 to)
        {
            if (!IsPointClear(from) || !IsPointClear(to))
                return false;
            Vector2 delta = to - from;
            foreach (Rect obstacle in obstacles)
            {
                float enter = 0f;
                float exit = 1f;
                if (IntersectsAxis(from.x, delta.x, obstacle.xMin, obstacle.xMax, ref enter, ref exit) &&
                    IntersectsAxis(from.y, delta.y, obstacle.yMin, obstacle.yMax, ref enter, ref exit))
                    return false;
            }
            return true;
        }

        public bool TryBuild(Vector2 from, Vector2 destination, List<Vector2> path)
        {
            path.Clear();
            if (!IsPointClear(from))
                return false;
            if (IsSegmentClear(from, destination))
            {
                path.Add(destination);
                return true;
            }

            nodes.Clear();
            nodes.Add(from);
            bool destinationClear = IsPointClear(destination);
            if (destinationClear)
                nodes.Add(destination);
            foreach (Rect obstacle in obstacles)
            {
                // Leave a small gap at corners so the path never grazes a collider.
                AddCorner(new Vector2(obstacle.xMin - 0.06f, obstacle.yMin - 0.06f));
                AddCorner(new Vector2(obstacle.xMin - 0.06f, obstacle.yMax + 0.06f));
                AddCorner(new Vector2(obstacle.xMax + 0.06f, obstacle.yMin - 0.06f));
                AddCorner(new Vector2(obstacle.xMax + 0.06f, obstacle.yMax + 0.06f));
            }

            var costs = new float[nodes.Count];
            var parents = new int[nodes.Count];
            var visited = new bool[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                costs[i] = float.PositiveInfinity;
                parents[i] = -1;
            }
            costs[0] = 0f;
            for (int iteration = 0; iteration < nodes.Count; iteration++)
            {
                int current = -1;
                float best = float.PositiveInfinity;
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (!visited[i] && costs[i] < best)
                    {
                        best = costs[i];
                        current = i;
                    }
                }
                if (current < 0)
                    break;
                visited[current] = true;
                if (destinationClear && current == 1)
                    break;
                for (int next = 1; next < nodes.Count; next++)
                {
                    float cost = best + Vector2.Distance(nodes[current], nodes[next]);
                    if (visited[next] || cost >= costs[next] || !IsSegmentClear(nodes[current], nodes[next]))
                        continue;
                    costs[next] = cost;
                    parents[next] = current;
                }
            }

            int end = destinationClear ? 1 : -1;
            if (!destinationClear)
            {
                // A rock may cover an old waypoint. Use the closest reachable edge instead.
                float nearest = float.PositiveInfinity;
                for (int i = 1; i < nodes.Count; i++)
                {
                    float distance = (nodes[i] - destination).sqrMagnitude;
                    if (visited[i] && distance < nearest)
                    {
                        nearest = distance;
                        end = i;
                    }
                }
            }
            if (end < 0 || float.IsPositiveInfinity(costs[end]))
                return false;
            while (end != 0)
            {
                path.Add(nodes[end]);
                end = parents[end];
            }
            path.Reverse();
            return path.Count > 0;
        }

        private void AddCorner(Vector2 point)
        {
            if (IsPointClear(point))
                nodes.Add(point);
        }

        private static bool IntersectsAxis(float origin, float direction, float min, float max,
            ref float enter, ref float exit)
        {
            if (Mathf.Abs(direction) < 0.00001f)
                return origin >= min && origin <= max;
            float first = (min - origin) / direction;
            float second = (max - origin) / direction;
            enter = Mathf.Max(enter, Mathf.Min(first, second));
            exit = Mathf.Min(exit, Mathf.Max(first, second));
            return enter <= exit;
        }
    }
}
