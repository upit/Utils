Shader "UI/UIImage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] [Toggle(UNITY_UI_CLIP_RECT)] _UseUIClipRect ("Use UI Clip Rect", Float) = 0
        [Toggle(USE_SAMPLE_ADD)] _FontMaterial ("Use sample add", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite off
        ZTest off //[unity_GUIZTestMode]
        Fog { Mode Off }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma target 2.0
            
            #include "UnityCG.cginc"
            #pragma shader_feature_local UNITY_UI_CLIP_RECT
            #pragma shader_feature_local USE_SAMPLE_ADD

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                half4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                half2 texcoord  : TEXCOORD0;
#ifdef UNITY_UI_CLIP_RECT
                half4 worldPosition : TEXCOORD1;
#endif
            };

            sampler2D _MainTex;
#ifdef USE_SAMPLE_ADD
            fixed4 _TextureSampleAdd;   // font
#endif
#ifdef UNITY_UI_CLIP_RECT
            float4 _ClipRect;
#endif
            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
#ifdef UNITY_UI_CLIP_RECT
                o.worldPosition = v.vertex;
#endif
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord; //TRANSFORM_TEX(v.texcoord, _MainTex);

                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half4 color = (tex2D(_MainTex, i.texcoord)
#ifdef USE_SAMPLE_ADD                
                + _TextureSampleAdd
#endif
                ) * i.color;

#ifdef UNITY_UI_CLIP_RECT
                half2 pos = i.worldPosition.xy;
                half2 inside = step(_ClipRect.xy, pos) * step(pos, _ClipRect.zw);
                color.a*= inside.x * inside.y;
#endif

                return color;
            }
        ENDCG
        }
    }
}