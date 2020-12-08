Shader "Fast Shadow Projector/Fast Shadow" {
    
    Properties
    {
     _MainTex ("Particle Texture", 2D) = "white" { }
     _ShadowIntensity ("Shadow Intensity", Range(0,1)) = 1
    }
    
    SubShader
    { 
        Tags { "QUEUE"="Transparent" "IGNOREPROJECTOR"="true" "RenderType"="Transparent" }
        
        Pass
        {
            ZWrite Off Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            Fog { Mode off }
        
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma target 2.0
            //#include "UnityCG.cginc"
            
            // vertex shader input data
            struct appdata {
              float3 pos  : POSITION;
              float2 uv  : TEXCOORD0;
            };
            
            // vertex-to-fragment interpolators
            struct v2f {
              float4 pos   : SV_POSITION;
              float2 uv   : TEXCOORD0;
            };
            
            // vertex shader
            v2f vert (appdata i) {
              v2f o;
              o.pos = UnityObjectToClipPos(i.pos);
              o.uv = i.uv;
              return o;
            }
            
            // textures
            sampler2D _MainTex;
            float _ShadowIntensity;
            
            // fragment shader
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D (_MainTex, i.uv);
                tex.a *= _ShadowIntensity;
                return tex;
            }

        ENDCG
        }
    }
}