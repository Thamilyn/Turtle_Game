using System.Collections.Generic;
using UnityEngine;

namespace TurtleGame
{
    /// <summary>Invisible beach checkpoints released permanently by the moon's progress.</summary>
    [DisallowMultipleComponent]
    public sealed class MoonProgressBarriers : MonoBehaviour
    {
        private readonly List<BoxCollider> gates = new List<BoxCollider>();
        private readonly List<float> releasePositions = new List<float>();

        public void Initialize(Terrain terrain, float startingZ, Vector2 destination, float spacing)
        {
            float destinationZ = destination.y;
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            // Leave room for the whole turtle and follow camera before the terrain drops away.
            float sideInset = Mathf.Min(8f, size.x * 0.2f);
            float endInset = Mathf.Min(6f, size.z * 0.1f);
            float leftEdge = origin.x + sideInset;
            float rightEdge = origin.x + size.x - sideInset;
            float rearEdge = origin.z + endInset;
            float playableWidth = rightEdge - leftEdge;
            float roadStart = destinationZ - 2.5f;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var collider in root.GetComponentsInChildren<BoxCollider>())
                {
                    if (collider.name == "Coastal road")
                        roadStart = collider.bounds.min.z;
                }
            }

            // Extend below and above all dunes; the walls have no renderer or trigger.
            float height = size.y + 20f;
            float centerY = origin.y + height * 0.5f - 5f;
            float centerX = origin.x + size.x * 0.5f;
            float finalGateZ = roadStart - 0.3f;
            spacing = Mathf.Max(3f, spacing);
            float firstZ = (Mathf.Floor(startingZ / spacing) + 1f) * spacing;
            for (float z = firstZ; z < finalGateZ - 2f; z += spacing)
                AddGate("Moon progress gate " + z.ToString("0.##") + " m", centerX, centerY, z,
                    playableWidth, height, z);

            // This last gate opens only once the moon is already above the asphalt.
            AddGate("Road approach gate", centerX, centerY, finalGateZ,
                playableWidth, height, roadStart + 0.2f);

            // Close the ends of the checkpoints so the player cannot run around the beach edges.
            float sideEnd = Mathf.Min(origin.z + size.z - endInset, destinationZ + 8f);
            float sideLength = sideEnd - rearEdge + 1f;
            float centerZ = rearEdge - 0.5f + sideLength * 0.5f;
            CreateWall("Beach left boundary", new Vector3(leftEdge - 0.25f, centerY, centerZ),
                new Vector3(0.5f, height, sideLength));
            CreateWall("Beach right boundary", new Vector3(rightEdge + 0.25f, centerY, centerZ),
                new Vector3(0.5f, height, sideLength));
            CreateWall("Beach rear boundary", new Vector3(centerX, centerY, rearEdge - 0.25f),
                new Vector3(playableWidth + 1f, height, 0.5f));
            CreateWall("Beach forward boundary", new Vector3(centerX, centerY, sideEnd + 0.25f),
                new Vector3(playableWidth + 1f, height, 0.5f));
            CreateRoadApproach(leftEdge, rightEdge, destination.x, finalGateZ,
                sideEnd, centerY, height);
            ReleaseReached(startingZ);
        }

        private void CreateRoadApproach(float leftEdge, float rightEdge, float moonX, float entranceZ,
            float endZ, float centerY, float height)
        {
            const float halfWidth = 1.6f;
            const float thickness = 0.3f;
            float approachZ = entranceZ - 12f;
            // The wide beach gradually converges on the moon's final position.
            CreateAngledWall("Road approach left funnel", new Vector2(leftEdge - 0.25f, approachZ),
                new Vector2(moonX - halfWidth, entranceZ), centerY, height, thickness);
            CreateAngledWall("Road approach right funnel", new Vector2(rightEdge + 0.25f, approachZ),
                new Vector2(moonX + halfWidth, entranceZ), centerY, height, thickness);

            // Keep the rest of the sand/asphalt border closed after the timed gate opens.
            float leftWidth = moonX - halfWidth - leftEdge;
            float rightWidth = rightEdge - moonX - halfWidth;
            if (leftWidth > 0f)
                CreateWall("Road left closed border", new Vector3(leftEdge + leftWidth * 0.5f, centerY, entranceZ),
                    new Vector3(leftWidth, height, thickness));
            if (rightWidth > 0f)
                CreateWall("Road right closed border", new Vector3(moonX + halfWidth + rightWidth * 0.5f, centerY, entranceZ),
                    new Vector3(rightWidth, height, thickness));
            CreateAngledWall("Road approach left corridor", new Vector2(moonX - halfWidth, entranceZ),
                new Vector2(moonX - halfWidth, endZ), centerY, height, thickness);
            CreateAngledWall("Road approach right corridor", new Vector2(moonX + halfWidth, entranceZ),
                new Vector2(moonX + halfWidth, endZ), centerY, height, thickness);
        }

        private void CreateAngledWall(string name, Vector2 from, Vector2 to, float centerY, float height, float thickness)
        {
            Vector2 center = (from + to) * 0.5f;
            Vector2 direction = to - from;
            var wall = CreateWall(name, new Vector3(center.x, centerY, center.y),
                new Vector3(thickness, height, direction.magnitude + thickness));
            wall.transform.rotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y));
        }

        public void ReleaseReached(float moonZ)
        {
            for (int i = 0; i < gates.Count; i++)
            {
                if (gates[i].enabled && moonZ >= releasePositions[i])
                    gates[i].enabled = false;
            }
        }

        private void AddGate(string name, float x, float y, float z, float width, float height, float releaseZ)
        {
            gates.Add(CreateWall(name, new Vector3(x, y, z), new Vector3(width + 1f, height, 0.3f)));
            releasePositions.Add(releaseZ);
        }

        private BoxCollider CreateWall(string name, Vector3 center, Vector3 size)
        {
            var wall = new GameObject(name);
            wall.transform.SetParent(transform, false);
            wall.transform.position = center;
            var collider = wall.AddComponent<BoxCollider>();
            collider.size = size;
            return collider;
        }
    }
}
