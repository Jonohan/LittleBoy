Shader "Hidden/Xuwu/Four Dimensional Portals/Write Stencil (Built-In)"
{
    Properties
    {
        [IntRange] _StencilRef("Stencil Ref", Range(1, 255)) = 1
    }

    SubShader
    {
        Pass
        {
            ColorMask 0
            Cull Back
            Stencil
            {
                Ref[_StencilRef]
                Comp Always
                Pass Replace
            }
            ZTest Always
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = UnityObjectToClipPos(IN.positionOS.xyz);

                return OUT;
            }

            float4 frag(Varyings IN) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
