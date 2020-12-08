Shader "UI/Effects/Fast Blur"
{
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_Parameter ("Blur pixel Offset",Range(0, 100))=0
	}

		CGINCLUDE

		#include "UnityCG.cginc"

		uniform sampler2D _MainTex;
		uniform half _Parameter;

		struct v2f_withBlurCoords8 
		{
			float4 pos : SV_POSITION;
			half4 uv : TEXCOORD0;
		};	

		v2f_withBlurCoords8 vertBlurVertical (appdata_img v)
		{
			v2f_withBlurCoords8 o;
			o.pos = UnityObjectToClipPos (v.vertex);
			o.uv.xy = half2(v.texcoord.xy);
			o.uv.zw = half2(0,_Parameter/_ScreenParams.x);

			return o; 
		}

		v2f_withBlurCoords8 vertBlurHorizontal (appdata_img v)
		{
			v2f_withBlurCoords8 o;
			o.pos = UnityObjectToClipPos (v.vertex);
			o.uv.xy = half2(v.texcoord.xy);
			o.uv.zw = half2(_Parameter/_ScreenParams.y,0);
			return o; 
		}	

		half4 fragBlur8 ( v2f_withBlurCoords8 i ) : SV_Target
		{
			half2 coords = i.uv.xy;
			half4 color = half4(0,0,0,1);
			half2 offsetStep=i.uv.zw;
			half2 curOffset=offsetStep*3.0h;

            half curve[7] = { 0.0205, 0.0855, 0.232, 0.324, 0.232, 0.0855, 0.0205 };
  			for( int j = 0; j < 7; j++ )
  			{
				half4 tap = tex2D(_MainTex, coords+curOffset);
				color.rgb += tap.rgb * curve[j];
				curOffset-=offsetStep;
  			}
			return color;
		}

		ENDCG 

	SubShader {
	  Tags {"Queue"="Geometry" "IgnoreProjector"="True" "RenderType"="Opaque" }
	  ZTest Always Cull Off Blend Off
	  ZWrite on

	// 0
	Pass 
	{
		CGPROGRAM 
		
		#pragma vertex vertBlurHorizontal
		#pragma fragment fragBlur8
		#pragma fragmentoption ARB_precision_hint_fastest
        #pragma target 2.0
		
		ENDCG
	}
	// 1
	Pass 
	{
		CGPROGRAM 
		
		#pragma vertex vertBlurVertical
		#pragma fragment fragBlur8
		#pragma fragmentoption ARB_precision_hint_fastest
        #pragma target 2.0
		ENDCG
	}
	}	
	Fallback "Diffuse"
}

