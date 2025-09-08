#if USING_UNIVERSAL_RENDER_PIPELINE
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Xuwu.FourDimensionalPortals
{
    partial class PortalSystem
    {
        private class DrawPenetratingViewMeshPass : ScriptableRenderPass
        {
            private readonly ProfilingSampler _profilingSampler;

            public Portal PenetratingPortal = null;
            public Material WriteStencilMaterial = null;
            public Material StencilViewMaterial = null;

            public DrawPenetratingViewMeshPass()
            {
                profilingSampler = new ProfilingSampler(nameof(DrawPenetratingViewMeshPass));
                _profilingSampler = new ProfilingSampler("DrawPenetratingViewMesh");
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (!PenetratingPortal || !WriteStencilMaterial || !StencilViewMaterial)
                    return;

                CommandBuffer cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, _profilingSampler))
                {
                    var planeMesh = PenetratingPortal.Config.PlaneMesh;
                    var viewMesh = PenetratingPortal.Config.PenetratingViewMesh;
                    var localToWorldMatrix = PenetratingPortal.transform.localToWorldMatrix;

                    cmd.ClearRenderTarget(RTClearFlags.Stencil, Color.clear, 1f, 0);
                    cmd.DrawMesh(planeMesh, localToWorldMatrix, WriteStencilMaterial, 0, 0);
                    cmd.DrawMesh(viewMesh, localToWorldMatrix, WriteStencilMaterial, 0, 0);
                    cmd.DrawMesh(viewMesh, localToWorldMatrix, StencilViewMaterial, 0, 0);
                    cmd.ClearRenderTarget(RTClearFlags.Stencil, Color.clear, 1f, 0);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
#endif
