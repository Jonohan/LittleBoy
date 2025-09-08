using System;
using UnityEngine;

namespace Xuwu.FourDimensionalPortals
{
    public static class PortalSystemUtils
    {
        public static bool Approximately(float a, float b) => Mathf.Abs(b - a) < PortalSystem.Tolerance;

        public static bool IsScalingUniform(Vector3 scale) => Approximately(scale.x, scale.y) && Approximately(scale.y, scale.z);

        // Adjusts the given projection matrix so that near plane is the given clipPlane
        // clipPlane is given in camera space. See article in Game Programming Gems 5 and
        // http://aras-p.info/texts/obliqueortho.html
        public static void CalculateObliqueMatrix(ref Matrix4x4 projection, Vector4 clipPlane)
        {
            Vector4 q = projection.inverse * new Vector4(
                clipPlane.x == 0f ? 0f : Mathf.Sign(clipPlane.x),
                clipPlane.y == 0f ? 0f : Mathf.Sign(clipPlane.y),
                1.0f,
                1.0f
            );
            Vector4 c = clipPlane * (2.0f / Vector4.Dot(clipPlane, q));
            // third row = clip plane - fourth row
            projection[2] = c.x - projection[3];
            projection[6] = c.y - projection[7];
            projection[10] = c.z - projection[11];
            projection[14] = c.w - projection[15];
        }

        public static Rect CalculateBoundsViewportRect(Matrix4x4 localToCameraMatrix, Matrix4x4 projectionMatrix, float zNear, Bounds localBounds)
        {
            var center = localBounds.center;
            var extents = localBounds.extents;

            Span<Vector3> corners = stackalloc Vector3[8];

            corners[0] = localToCameraMatrix.MultiplyPoint3x4(localBounds.min);
            corners[1] = localToCameraMatrix.MultiplyPoint3x4(new Vector3(center.x - extents.x, center.y - extents.y, center.z + extents.z));
            corners[2] = localToCameraMatrix.MultiplyPoint3x4(new Vector3(center.x - extents.x, center.y + extents.y, center.z - extents.z));
            corners[3] = localToCameraMatrix.MultiplyPoint3x4(new Vector3(center.x - extents.x, center.y + extents.y, center.z + extents.z));
            corners[4] = localToCameraMatrix.MultiplyPoint3x4(new Vector3(center.x + extents.x, center.y - extents.y, center.z - extents.z));
            corners[5] = localToCameraMatrix.MultiplyPoint3x4(new Vector3(center.x + extents.x, center.y - extents.y, center.z + extents.z));
            corners[6] = localToCameraMatrix.MultiplyPoint3x4(new Vector3(center.x + extents.x, center.y + extents.y, center.z - extents.z));
            corners[7] = localToCameraMatrix.MultiplyPoint3x4(localBounds.max);

            var min = Vector2.positiveInfinity;
            var max = Vector2.negativeInfinity;
            bool isVisible = false;

            foreach (var cornerVS in corners)
            {
                if (cornerVS.z <= -zNear)
                {
                    SetMinMax(projectionMatrix.MultiplyPoint(cornerVS));
                    continue;
                }

                foreach (var otherCornerVS in corners)
                {
                    if (otherCornerVS.z > -zNear)
                        continue;

                    isVisible = true;

                    Vector3 cornerToOtherCorner = otherCornerVS - cornerVS;
                    Vector3 projectOnNearPlane = cornerVS + (zNear + cornerVS.z) / Vector3.Dot(cornerToOtherCorner, Vector3.back) * cornerToOtherCorner;

                    SetMinMax(projectionMatrix.MultiplyPoint(projectOnNearPlane));
                }

                if (!isVisible)
                {
                    SetMinMax(Vector2.zero);
                    SetMinMax(Vector2.one);
                    break;
                }
            }

            float xmin = Mathf.Clamp01(min.x * .5f + .5f);
            float ymin = Mathf.Clamp01(min.y * .5f + .5f);
            float xmax = Mathf.Clamp01(max.x * .5f + .5f);
            float ymax = Mathf.Clamp01(max.y * .5f + .5f);

            return Rect.MinMaxRect(xmin, ymin, xmax, ymax);

            void SetMinMax(Vector2 position)
            {
                min = Vector2.Min(min, position);
                max = Vector2.Max(max, position);
            }
        }

        public static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj != null)
            {
#if UNITY_EDITOR
                if (Application.isPlaying && !UnityEditor.EditorApplication.isPaused)
                    UnityEngine.Object.Destroy(obj);
                else
                    UnityEngine.Object.DestroyImmediate(obj);
#else
                UnityEngine.Object.Destroy(obj);
#endif
            }
        }
    }
}
