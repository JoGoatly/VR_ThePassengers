// PSX-style shader for the Built-in Render Pipeline.
//
// - Vertex snapping (wobbly PS1 geometry)
// - Optional affine texture mapping (PS1 texture warping)
// - Per-vertex (Gouraud) lighting incl. up to 4 point lights
// - Color depth reduction (15-bit look)
// - Glass: every texel with alpha below _GlassThreshold is rendered as
//   transparent glass in a second pass, everything else is opaque.
//   This lets one texture/material contain both the bus body and its windows.
Shader "PSX/Lit Glass"
{
    Properties
    {
        _MainTex ("Texture (RGB, A = glass)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Glass)]
        _GlassColor ("Glass Tint", Color) = (0.75, 0.85, 0.9, 1)
        _GlassOpacity ("Glass Opacity Multiplier", Range(0, 2)) = 0.8
        _GlassThreshold ("Glass Alpha Threshold", Range(0.01, 1)) = 0.85

        [Header(PSX)]
        _SnapResolution ("Vertex Snap Resolution", Vector) = (320, 240, 0, 0)
        _AffineAmount ("Affine Texture Warping", Range(0, 1)) = 0
        _ColorSteps ("Color Steps per Channel", Range(2, 256)) = 32

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (opaque parts)", Float) = 2
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    #include "Lighting.cginc"

    sampler2D _MainTex;
    float4 _MainTex_ST;
    fixed4 _Color;
    fixed4 _GlassColor;
    half _GlassOpacity;
    half _GlassThreshold;
    float4 _SnapResolution;
    half _AffineAmount;
    half _ColorSteps;

    struct appdata
    {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float2 uv : TEXCOORD0;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 affineUV : TEXCOORD1; // xy = uv * w, z = w
        fixed3 light : TEXCOORD2;
        UNITY_FOG_COORDS(3)
    };

    float4 SnapVertex(float4 clipPos)
    {
        float2 res = max(_SnapResolution.xy, 1.0) * 0.5;
        clipPos.xy = round(clipPos.xy / clipPos.w * res) / res * clipPos.w;
        return clipPos;
    }

    v2f vert(appdata v)
    {
        v2f o;
        o.pos = SnapVertex(UnityObjectToClipPos(v.vertex));
        o.uv = TRANSFORM_TEX(v.uv, _MainTex);
        o.affineUV = float3(o.uv * o.pos.w, o.pos.w);

        float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
        float3 worldNormal = UnityObjectToWorldNormal(v.normal);

        // Gouraud lighting: main directional light + ambient + vertex point lights
        float3 lightDir = normalize(_WorldSpaceLightPos0.xyz - worldPos * _WorldSpaceLightPos0.w);
        fixed3 light = _LightColor0.rgb * saturate(dot(worldNormal, lightDir));
        light += ShadeSH9(float4(worldNormal, 1));
        #ifdef VERTEXLIGHT_ON
        light += Shade4PointLights(
            unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
            unity_LightColor[0].rgb, unity_LightColor[1].rgb,
            unity_LightColor[2].rgb, unity_LightColor[3].rgb,
            unity_4LightAtten0, worldPos, worldNormal);
        #endif
        o.light = light;

        UNITY_TRANSFER_FOG(o, o.pos);
        return o;
    }

    fixed4 SampleTex(v2f i)
    {
        float2 affine = i.affineUV.xy / i.affineUV.z;
        return tex2D(_MainTex, lerp(i.uv, affine, _AffineAmount));
    }

    fixed3 Posterize(fixed3 c)
    {
        return floor(c * _ColorSteps + 0.5) / _ColorSteps;
    }

    fixed4 fragOpaque(v2f i) : SV_Target
    {
        fixed4 tex = SampleTex(i);
        clip(tex.a - _GlassThreshold);

        fixed4 col = fixed4(tex.rgb * _Color.rgb * i.light, 1);
        col.rgb = Posterize(col.rgb);
        UNITY_APPLY_FOG(i.fogCoord, col);
        return col;
    }

    fixed4 fragGlass(v2f i) : SV_Target
    {
        fixed4 tex = SampleTex(i);
        // Opaque texels are drawn by the first pass; alpha 0 texels are holes.
        clip(_GlassThreshold - tex.a);
        clip(tex.a - 0.004);

        fixed4 col;
        col.rgb = tex.rgb * _GlassColor.rgb * i.light;
        col.a = saturate(tex.a * _GlassColor.a * _GlassOpacity);
        col.rgb = Posterize(col.rgb);
        UNITY_APPLY_FOG(i.fogCoord, col);
        return col;
    }
    ENDCG

    SubShader
    {
        // Transparent queue so the glass is blended over the skybox and all
        // opaque objects. The opaque part still writes depth.
        Tags { "Queue" = "Transparent" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }

        Pass
        {
            Name "OPAQUE"
            Tags { "LightMode" = "ForwardBase" }
            Cull [_Cull]
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragOpaque
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            ENDCG
        }

        Pass
        {
            Name "GLASS"
            Tags { "LightMode" = "ForwardBase" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragGlass
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull [_Cull]

            CGPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_shadowcaster

            struct v2fShadow
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };

            v2fShadow vertShadow(appdata_base v)
            {
                v2fShadow o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            float4 fragShadow(v2fShadow i) : SV_Target
            {
                // Glass does not cast shadows, so light shines into the bus.
                clip(tex2D(_MainTex, i.uv).a - _GlassThreshold);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    FallBack Off
}
