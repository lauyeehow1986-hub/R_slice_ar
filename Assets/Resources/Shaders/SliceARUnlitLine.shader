Shader "SliceAR/UnlitLine"
{
    // Flat, unlit, vertex-coloured transparent shader for the AR volume-bounds wireframe
    // (see ARVolumeOutline). Nothing about a positional cue should react to lighting or shading, so
    // this deliberately does no more than pass the line's own colour through.
    //
    // It lives under Assets/Resources rather than being registered in Graphics ▸ Always Included
    // Shaders (which is how its sibling Assets/Shaders/SliceRenderingTransparent.shader ships).
    // Both guarantee inclusion in the build, which Shader.Find alone does not; Resources gets there
    // without editing ProjectSettings, and ProjectSettings edits on this project have a history of
    // taking AR down with them.
    Properties
    {
        _Color("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal" }
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

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
                half4 color : COLOR;
            };

            struct v2f
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.color = v.color * _Color;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                return i.color;
            }
            ENDHLSL
        }
    }
}
