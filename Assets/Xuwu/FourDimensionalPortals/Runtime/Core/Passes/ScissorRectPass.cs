#if USING_UNIVERSAL_RENDER_PIPELINE
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Xuwu.FourDimensionalPortals
{
    partial class PortalSystem
    {
        private class ScissorRectPass : ScriptableRenderPass
        {
            public Rect ViewportRect = default;

            public ScissorRectPass()
            {
                profilingSampler = new ProfilingSampler(nameof(ScissorRectPass));
                renderPassEvent = RenderPassEvent.BeforeRenderingPrePasses;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                var desc = renderingData.cameraData.cameraTargetDescriptor;
                var scissorRect = new Rect(ViewportRect.x * desc.width, ViewportRect.y * desc.height,
                    ViewportRect.width * desc.width, ViewportRect.height * desc.height);

                cmd.EnableScissorRect(scissorRect);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnFinishCameraStackRendering(CommandBuffer cmd) => cmd.DisableScissorRect();
        }
    }
}
#endif
