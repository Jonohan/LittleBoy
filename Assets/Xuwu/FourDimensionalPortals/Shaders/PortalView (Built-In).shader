Shader "Hidden/Xuwu/Four Dimensional Portals/Portal View (Built-In)"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull Mode", Float) = 2
        [IntRange] _StencilRef("Stencil Ref", Range(0, 255)) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil Comp", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4
        [Enum(Off, 0, On, 1)] _ZWrite("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "DisableBatching" = "True"
            "ForceNoShadowCasting" = "True"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "PortalView"

            Cull [_CullMode]
            Stencil
            {
                Ref [_StencilRef]
                Comp [_StencilComp]
            }
            ZTest [_ZTest]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
            UNITY_DECLARE_TEX2D_NOSAMPLER(_MainTex);
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = UnityObjectToClipPos(IN.positionOS.xyz);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_TARGET
            {
                return _MainTex.Load(int3(IN.positionCS.xy, 0));
            }
            ENDHLSL
        }

        UsePass "Standard/ShadowCaster"
    }
}
