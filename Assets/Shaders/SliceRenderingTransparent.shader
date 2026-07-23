Shader "SliceAR/SliceRenderingTransparent"
{
    // A drop-in variant of UnityVolumeRendering's URP SliceRenderingShader that makes fragments
    // OUTSIDE the volume (and air/background inside the bounding box) TRANSPARENT instead of opaque
    // black. The stock shader returns half4(0,0,0,1) there, which paints the whole slice quad black
    // over the AR camera passthrough (so AR Slice mode looked like it had lost the camera feed). With
    // the transparent regions discarded, the passthrough shows through and only the tissue slice is
    // drawn. In the 3D CT-viewer the background is black anyway, so it looks identical there.
    // Assigned at runtime by SliceController.Setup; kept in Assets/ (not a fork of the package).
    Properties
    {
        _DataTex("Data Texture (Generated)", 3D) = "" {}
        _TFTex("Transfer Function Texture", 2D) = "white" {}
    }
    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal" }
        Tags { "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 relVert : TEXCOORD1;
            };

            Texture3D _DataTex;             SamplerState sampler_DataTex;
            Texture2D _TFTex;               SamplerState sampler_TFTex;

            uniform float4x4 _parentInverseMat;
            uniform float4x4 _planeMat;

            v2f vert (appdata v)
            {
                v2f o;
                float3 worldPos = TransformObjectToWorld(v.vertex.xyz);
                o.vertex = TransformWorldToHClip(worldPos);
                float2 uvMod = float2((0.5f - v.uv.x) * 10.0f, (0.5f - v.uv.y) * 10.0f);
                float3 vert = mul(_planeMat, float4(uvMod.x, 0.0f, uvMod.y, 1.0f));
                o.relVert = mul(_parentInverseMat, float4(vert, 1.0f));
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float3 dataCoord = i.relVert + float3(0.5f, 0.5f, 0.5f);
                // Outside the volume bounding box: transparent (let the AR passthrough / 3D background show).
                if (dataCoord.x > 1.0f || dataCoord.y > 1.0f || dataCoord.z > 1.0f ||
                    dataCoord.x < 0.0f || dataCoord.y < 0.0f || dataCoord.z < 0.0f)
                    discard;

                float dataVal = _DataTex.Sample(sampler_DataTex, dataCoord);
                half4 col = _TFTex.Sample(sampler_TFTex, float2(dataVal, 0.0));
                // Air / background inside the box (transfer-function alpha ~0): also transparent, so the
                // slice reads as just the tissue silhouette rather than a black rectangle in AR.
                if (col.a < 0.02f)
                    discard;
                col.a = 1.0f;   // tissue is drawn opaque (crisp CT/MRI slice)
                return col;
            }
            ENDHLSL
        }
    }
}
