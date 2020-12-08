 Shader "Fast Shadow Projector/Projector Multiply"
 {
     Properties
     {
         _ShadowTex ("Cookie", 2D) = "gray"
     }
      
     Subshader
     {
         Tags { "RenderType"="Transparent" "Queue"="Transparent-2" }
         Pass
         {
             ZWrite Off
             ColorMask RGB
             Blend DstColor Zero
			 Offset -12, 0
			 //Fog { Color (1, 1, 1) }
                          
            CGPROGRAM
             #include "UnityCG.cginc"
             
             #pragma vertex vert
             #pragma fragment frag
             #pragma fragmentoption ARB_precision_hint_fastest
                 
             struct v2f
             {
                 float4 pos : SV_POSITION;
                 float4 uv_Main : TEXCOORD0;
                 float4 uv_MainClip : TEXCOORD1;
             };
             
             sampler2D _ShadowTex;
             float4x4 _GlobalProjector;
             float4x4 _GlobalProjectorClip;
              
             v2f vert(appdata_tan v)
             {
                 v2f o;
                 o.pos = UnityObjectToClipPos (v.vertex);
                 o.uv_Main = mul (_GlobalProjector, v.vertex);
                 o.uv_MainClip = mul (_GlobalProjectorClip, v.vertex);
                 return o;
             }
             
             half4 frag (v2f i) : COLOR
             {
                clip(3-i.uv_MainClip.w);
                //if (i.uv_MainClip.w > 0.0f) {return half4(1, 1, 1, 1);}
                return tex2D(_ShadowTex, i.uv_Main.xy);
             }
             ENDCG
      
         }
     }
 }